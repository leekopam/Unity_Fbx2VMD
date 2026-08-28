using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fbx2Vmd.Settings
{
    [DisallowMultipleComponent]
    public sealed class MainRecordingSettingsCompanionController : MonoBehaviour
    {
        [SerializeField] private string settingsFilePathOverride;
        [SerializeField] private TMP_InputField fbxPathInput;
        [SerializeField] private TMP_InputField characterModelPathInput;
        [SerializeField] private TMP_InputField captureWidthInput;
        [SerializeField] private TMP_InputField captureHeightInput;
        [SerializeField] private Toggle openSettingsOnStartToggle;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button importFbxButton;
        [SerializeField] private TextMeshProUGUI feedbackText;

        private MainRecordingSettingsStore store;
        private MainRecordingSettingsDocument document;
        private string statusMessage = string.Empty;
        public MainRecordingSettingsDocument CurrentDocument => EnsureDocument();
        public string StatusMessage => statusMessage;

        private void Awake()
        {
            ApplyReadableKoreanFonts();
            BindSaveButton();
            BindImportFbxButton();
            LoadSettings();
        }

        private void OnEnable()
        {
            ApplyReadableKoreanFonts();
            RefreshUiFromDocument();
            RefreshSaveButtonState();
        }

        public void LoadSettings()
        {
            try
            {
                store = CreateStore();
                document = store.LoadOrCreateDefault();
                RefreshUiFromDocument();
                SetStatus("설정을 불러왔습니다.");
            }
            catch (Exception exception)
            {
                document = new MainRecordingSettingsDocument();
                RefreshUiFromDocument();
                SetStatus("설정 로드에 실패했습니다.");
                Debug.LogWarning("[MainRecordingSettingsCompanionController] " + exception.Message);
            }
            finally
            {
                RefreshSaveButtonState();
            }
        }

        public bool SaveSettings()
        {
            try
            {
                store = CreateStore();
                ApplyUiToDocument();
                store.Save(EnsureDocument());
                SetStatus("설정을 저장했습니다.");
                RefreshSaveButtonState();
                return true;
            }
            catch (Exception exception)
            {
                SetStatus("설정 저장에 실패했습니다.");
                Debug.LogWarning("[MainRecordingSettingsCompanionController] " + exception.Message);
                RefreshSaveButtonState();
                return false;
            }
        }

        public bool SaveImportFbxCommand()
        {
            try
            {
                store = CreateStore();
                ApplyUiToDocument();

                MainRecordingSettingsDocument current = EnsureDocument();
                string fbxPath = string.IsNullOrWhiteSpace(current.fbxPath)
                    ? string.Empty
                    : current.fbxPath.Trim();
                if (string.IsNullOrEmpty(fbxPath))
                {
                    SetStatus("FBX 경로가 비어 있습니다.");
                    RefreshSaveButtonState();
                    return false;
                }

                current.pendingCommand = new MainRecordingSettingsCommandEnvelope
                {
                    commandId = Guid.NewGuid().ToString("N"),
                    action = MainRecordingSettingsCommandEnvelope.ImportFbxAction,
                    fbxPath = fbxPath,
                    requestedAtUtc = DateTime.UtcNow.ToString("O"),
                };

                store.Save(current);
                SetStatus("FBX 가져오기 명령을 저장했습니다.");
                RefreshSaveButtonState();
                return true;
            }
            catch (Exception exception)
            {
                SetStatus("FBX 가져오기 명령 저장에 실패했습니다.");
                Debug.LogWarning("[MainRecordingSettingsCompanionController] " + exception.Message);
                RefreshSaveButtonState();
                return false;
            }
        }

        private MainRecordingSettingsStore CreateStore()
        {
            return new MainRecordingSettingsStore(settingsFilePathOverride);
        }

        private MainRecordingSettingsDocument EnsureDocument()
        {
            return document ?? (document = new MainRecordingSettingsDocument());
        }

        private void BindSaveButton()
        {
            if (saveButton == null)
            {
                return;
            }

            saveButton.onClick.RemoveListener(HandleSaveButtonClicked);
            saveButton.onClick.AddListener(HandleSaveButtonClicked);
        }

        private void BindImportFbxButton()
        {
            if (importFbxButton == null)
            {
                return;
            }

            importFbxButton.onClick.RemoveListener(HandleImportFbxButtonClicked);
            importFbxButton.onClick.AddListener(HandleImportFbxButtonClicked);
        }

        private void HandleSaveButtonClicked()
        {
            SaveSettings();
        }

        private void HandleImportFbxButtonClicked()
        {
            SaveImportFbxCommand();
        }

        private void RefreshUiFromDocument()
        {
            MainRecordingSettingsDocument current = EnsureDocument();
            SetInputText(fbxPathInput, current.fbxPath);
            SetInputText(characterModelPathInput, current.characterModelPath);
            SetInputText(captureWidthInput, current.captureWidth.ToString());
            SetInputText(captureHeightInput, current.captureHeight.ToString());
            if (openSettingsOnStartToggle != null)
            {
                openSettingsOnStartToggle.isOn = current.openSettingsOnStart;
            }
        }

        private void ApplyUiToDocument()
        {
            MainRecordingSettingsDocument current = EnsureDocument();
            if (fbxPathInput != null)
            {
                current.fbxPath = fbxPathInput.text ?? string.Empty;
            }

            if (characterModelPathInput != null)
            {
                current.characterModelPath = characterModelPathInput.text ?? string.Empty;
            }

            if (TryReadPositiveInt(captureWidthInput, out int captureWidth))
            {
                current.captureWidth = captureWidth;
            }

            if (TryReadPositiveInt(captureHeightInput, out int captureHeight))
            {
                current.captureHeight = captureHeight;
            }

            if (openSettingsOnStartToggle != null)
            {
                current.openSettingsOnStart = openSettingsOnStartToggle.isOn;
            }
        }

        private void RefreshSaveButtonState()
        {
            if (saveButton != null)
            {
                saveButton.interactable = EnsureDocument() != null;
            }
        }

        private void SetStatus(string message)
        {
            statusMessage = message ?? string.Empty;
            if (feedbackText != null)
            {
                feedbackText.text = statusMessage;
                KoreanUiTextFallback.Apply(feedbackText);
            }
        }

        private void ApplyReadableKoreanFonts()
        {
            TextMeshProUGUI[] labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI label in labels)
            {
                KoreanUiTextFallback.Apply(label);
            }
        }

        private static void SetInputText(TMP_InputField input, string value)
        {
            if (input != null)
            {
                input.text = value ?? string.Empty;
            }
        }

        private static bool TryReadPositiveInt(TMP_InputField input, out int value)
        {
            value = 0;
            return input != null &&
                   int.TryParse(input.text, out value) &&
                   value > 0;
        }

        private MainRecordingSettingsDocument LoadFromPathForTests(string path)
        {
            settingsFilePathOverride = path;
            LoadSettings();
            return EnsureDocument();
        }

        private void SetDocumentForTests(MainRecordingSettingsDocument value)
        {
            document = value ?? new MainRecordingSettingsDocument();
            RefreshUiFromDocument();
            RefreshSaveButtonState();
        }

        private bool SaveCurrentDocumentForTests()
        {
            return SaveSettings();
        }

        private bool IsSaveButtonEnabledForTests()
        {
            RefreshSaveButtonState();
            return saveButton == null || saveButton.interactable;
        }

        private string GetStatusMessageForTests()
        {
            return statusMessage;
        }

        private bool HasReadableKoreanTextForTests()
        {
            ApplyReadableKoreanFonts();
            TextMeshProUGUI[] labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI label in labels)
            {
                if (!KoreanUiTextFallback.IsReadable(label))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
