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
        private const string PlayPauseButtonName = "FBX_PlayPause_Button";
        private const string StopButtonName = "FBX_Stop_Button";
        private const float ButtonVerticalSpacing = 110f;

        private FBXVmdPipeline _pipeline;
        private Button _playPauseButton;
        private Button _stopButton;

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
            DestroyControlButton(_stopButton);
        }

        private void Build(FBXVmdPipeline pipeline, Button template)
        {
            Bind(pipeline);
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
            _stopButton ??= FindButton(parent, StopButtonName);

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

        private void Refresh()
        {
            bool hasPreparedMotion =
                _pipeline != null && _pipeline.HasPreparedImportedMotion;
            if (_playPauseButton != null)
            {
                _playPauseButton.interactable = hasPreparedMotion;
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

        private static void SetLabel(Button button, string label)
        {
            TMP_Text tmpLabel = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpLabel != null)
            {
                tmpLabel.text = label;
                return;
            }

            Text legacyLabel = button.GetComponentInChildren<Text>(true);
            if (legacyLabel != null)
            {
                legacyLabel.text = label;
            }
        }

        private static Button ResolveImportButton()
        {
            GameObject buttonObject = GameObject.Find(ImportButtonName);
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
    }
}
