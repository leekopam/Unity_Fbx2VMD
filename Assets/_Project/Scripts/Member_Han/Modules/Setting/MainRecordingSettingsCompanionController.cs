using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Member_Han.Modules.Graphics
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
        private const string KoreanUiTextSample =
            "가나다FBX파일임포트선택해프로젝트로가져오고모션캡쳐설정을시작합니다공유설정을불러왔습니다저장했습니다시작시열기시네마토그래피환경캐릭터비활성화가로세로경로";
        private static readonly string[] KoreanUiFontNames =
        {
            "Malgun Gothic",
            "맑은 고딕",
            "Noto Sans KR",
            "Noto Sans CJK KR",
            "NanumGothic",
            "Nanum Gothic"
        };
        private static TMP_FontAsset cachedKoreanUiFont;
        private static Font cachedKoreanLegacyUiFont;
        private static bool warnedMissingKoreanUiFont;

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
                ApplyReadableKoreanFont(feedbackText);
            }
        }

        private void ApplyReadableKoreanFonts()
        {
            TextMeshProUGUI[] labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI label in labels)
            {
                ApplyReadableKoreanFont(label);
            }
        }

        private static void ApplyReadableKoreanFont(TextMeshProUGUI label)
        {
            if (label == null || !ContainsKorean(label.text) || FontAssetSupportsText(label.font, label.text))
            {
                return;
            }

            if (TryEnableLegacyKoreanText(label))
            {
                return;
            }

            TMP_FontAsset koreanFont = GetOrCreateKoreanUiFont();
            if (koreanFont == null || !FontAssetSupportsText(koreanFont, label.text))
            {
                if (!warnedMissingKoreanUiFont)
                {
                    warnedMissingKoreanUiFont = true;
                    Debug.LogWarning("[MainRecordingSettingsCompanionController] 한글 UI 폰트를 찾지 못했습니다. OS 한글 폰트 설치 상태를 확인하세요.");
                }

                return;
            }

            label.font = koreanFont;
            label.SetAllDirty();
        }

        private static bool TryEnableLegacyKoreanText(TextMeshProUGUI label)
        {
            Font legacyFont = GetOrCreateKoreanLegacyUiFont();
            if (legacyFont == null)
            {
                return false;
            }

            Transform existing = label.transform.Find("KoreanTextFallback");
            GameObject fallbackObject = existing != null
                ? existing.gameObject
                : new GameObject("KoreanTextFallback", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            fallbackObject.transform.SetParent(label.transform, false);

            RectTransform fallbackRect = fallbackObject.GetComponent<RectTransform>();
            fallbackRect.anchorMin = Vector2.zero;
            fallbackRect.anchorMax = Vector2.one;
            fallbackRect.offsetMin = Vector2.zero;
            fallbackRect.offsetMax = Vector2.zero;
            fallbackRect.localScale = Vector3.one;

            Text fallbackText = fallbackObject.GetComponent<Text>();
            fallbackText.text = label.text;
            fallbackText.font = legacyFont;
            fallbackText.fontSize = Mathf.Max(1, Mathf.RoundToInt(label.fontSize));
            fallbackText.fontStyle = (label.fontStyle & FontStyles.Bold) == FontStyles.Bold
                ? FontStyle.Bold
                : FontStyle.Normal;
            fallbackText.alignment = ConvertToLegacyAlignment(label.alignment);
            fallbackText.color = label.color;
            fallbackText.horizontalOverflow = HorizontalWrapMode.Wrap;
            fallbackText.verticalOverflow = VerticalWrapMode.Truncate;
            fallbackText.raycastTarget = false;
            fallbackText.supportRichText = true;
            fallbackText.enabled = true;

            label.enabled = false;
            return true;
        }

        private static TextAnchor ConvertToLegacyAlignment(TextAlignmentOptions alignment)
        {
            switch (alignment)
            {
                case TextAlignmentOptions.Center:
                    return TextAnchor.MiddleCenter;
                case TextAlignmentOptions.MidlineRight:
                    return TextAnchor.MiddleRight;
                case TextAlignmentOptions.TopLeft:
                    return TextAnchor.UpperLeft;
                case TextAlignmentOptions.MidlineLeft:
                default:
                    return TextAnchor.MiddleLeft;
            }
        }

        private static TMP_FontAsset GetOrCreateKoreanUiFont()
        {
            if (cachedKoreanUiFont != null)
            {
                return cachedKoreanUiFont;
            }

            Font osFont = Font.CreateDynamicFontFromOSFont(KoreanUiFontNames, 32);
            if (osFont == null)
            {
                return null;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(osFont);
            if (fontAsset == null)
            {
                return null;
            }

            fontAsset.name = "Main Recording Companion Korean UI Font";
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.TryAddCharacters(KoreanUiTextSample, out _);
            if (!FontAssetSupportsText(fontAsset, KoreanUiTextSample))
            {
                return null;
            }

            cachedKoreanUiFont = fontAsset;
            return cachedKoreanUiFont;
        }

        private static Font GetOrCreateKoreanLegacyUiFont()
        {
            if (cachedKoreanLegacyUiFont != null)
            {
                return cachedKoreanLegacyUiFont;
            }

            Font osFont = Font.CreateDynamicFontFromOSFont(KoreanUiFontNames, 32);
            if (osFont != null)
            {
                cachedKoreanLegacyUiFont = osFont;
            }

            return cachedKoreanLegacyUiFont;
        }

        private static bool FontAssetSupportsText(TMP_FontAsset fontAsset, string text)
        {
            if (fontAsset == null)
            {
                return false;
            }

            foreach (char character in text)
            {
                if (IsKorean(character) && !fontAsset.HasCharacter(character, true, true))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsKorean(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (char character in text)
            {
                if (IsKorean(character))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsKorean(char character)
        {
            return (character >= '\uAC00' && character <= '\uD7A3') ||
                   (character >= '\u3130' && character <= '\u318F') ||
                   (character >= '\u1100' && character <= '\u11FF');
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

        private bool SaveImportFbxCommandForTests()
        {
            return SaveImportFbxCommand();
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
                if (label == null || !ContainsKorean(label.text) || FontAssetSupportsText(label.font, label.text))
                {
                    continue;
                }

                Transform fallback = label.transform.Find("KoreanTextFallback");
                Text fallbackText = fallback != null ? fallback.GetComponent<Text>() : null;
                if (label.enabled || fallbackText == null || !fallbackText.enabled || fallbackText.font == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
