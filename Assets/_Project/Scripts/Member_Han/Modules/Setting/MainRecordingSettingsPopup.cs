using System.Collections.Generic;
using Member_Han.Modules.FBXImporter;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Member_Han.Modules.Graphics
{
    [DisallowMultipleComponent]
    public sealed class MainRecordingSettingsPopup : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] private bool openOnStart = true;
        [SerializeField] private RecodingSetting recodingSetting;
        [SerializeField] private FileManager fileManager;

        private readonly List<Button> cardButtons = new List<Button>();
        private RectTransform panelRoot;
        private CanvasGroup canvasGroup;
        private TextMeshProUGUI notificationText;
        private Vector2 dragStartAnchoredPosition;
        private Vector2 dragStartPointerPosition;
        private bool isOpen;
        private const string KoreanUiTextSample =
            "가나다FBX파일임포트선택프로젝트로가져오고모션캡쳐설정을시작합니다시네마토그래피환경준비중닫기";
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

        public bool IsOpen => isOpen;
        public bool OpenOnStart => openOnStart;
        public RecodingSetting RecodingSetting => recodingSetting;
        public FileManager FileManager => MainRecordingSettingsActions.ResolveFileManager(recodingSetting, fileManager);

        private void Awake()
        {
            EnsureBuilt();
        }

        private void Start()
        {
            if (openOnStart)
            {
                Open();
            }
        }

        public static MainRecordingSettingsPopup EnsurePopupForScene(RecodingSetting owner)
        {
            Canvas canvas = ResolveCanvas();
            MainRecordingSettingsPopup popup = canvas.GetComponentInChildren<MainRecordingSettingsPopup>(true);
            if (popup == null)
            {
                var popupObject = new GameObject(MainRecordingSettingsLayoutSpec.PopupObjectName, typeof(RectTransform));
                popupObject.layer = canvas.gameObject.layer;
                popupObject.transform.SetParent(canvas.transform, false);
                popup = popupObject.AddComponent<MainRecordingSettingsPopup>();
            }

            popup.Bind(owner, MainRecordingSettingsActions.ResolveFileManager(owner));
            popup.EnsureBuilt();
            return popup;
        }

        public void Bind(RecodingSetting owner, FileManager manager)
        {
            if (owner != null)
            {
                recodingSetting = owner;
            }

            if (manager != null)
            {
                fileManager = manager;
            }
        }

        public void Open()
        {
            EnsureBuilt();
            SetVisible(true);
            isOpen = true;
        }

        public void Close()
        {
            EnsureBuilt();
            SetVisible(false);
            isOpen = false;
        }

        public Vector2 GetReferenceSizeForTests()
        {
            return MainRecordingSettingsLayoutSpec.ReferenceSize;
        }

        public Vector2 GetDisplayedSizeForTests()
        {
            return MainRecordingSettingsLayoutSpec.DefaultDisplaySize;
        }

        public bool SupportsPointerDragForTests()
        {
            return true;
        }

        public void ApplyDragDeltaForTests(Vector2 delta)
        {
            EnsureBuilt();
            panelRoot.anchoredPosition += delta;
        }

        public int GetCardButtonCountForTests()
        {
            EnsureBuilt();
            return cardButtons.Count;
        }

        public bool CanResolveImportActionForTests()
        {
            return MainRecordingSettingsActions.CanExecute(
                MainRecordingSettingsActionType.ImportFbx,
                recodingSetting,
                fileManager);
        }

        public bool UsesCharacterVisualAssetForTests()
        {
            return false;
        }

        public bool IsProductionSurfaceForTests()
        {
            return false;
        }

        public bool HasReadableKoreanTextForTests()
        {
            EnsureBuilt();
            TextMeshProUGUI[] labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI label in labels)
            {
                if (ContainsKorean(label.text) && !FontAssetSupportsText(label.font, label.text))
                {
                    Transform fallback = label.transform.Find("KoreanTextFallback");
                    Text fallbackText = fallback != null ? fallback.GetComponent<Text>() : null;
                    if (label.enabled || fallbackText == null || !fallbackText.enabled || fallbackText.font == null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public string[] GetSidebarItemLabelsForTests()
        {
            MainRecordingSettingsSidebarItemSpec[] items = MainRecordingSettingsLayoutSpec.SidebarItems;
            var labels = new string[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                labels[i] = items[i].Label;
            }

            return labels;
        }

        public string[] GetVisibleTextForTests()
        {
            EnsureBuilt();
            var texts = new List<string>();
            TextMeshProUGUI[] textMeshLabels = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI label in textMeshLabels)
            {
                texts.Add(label.text);
            }

            Text[] legacyLabels = GetComponentsInChildren<Text>(true);
            foreach (Text label in legacyLabels)
            {
                texts.Add(label.text);
            }

            return texts.ToArray();
        }

        private static Canvas ResolveCanvas()
        {
            GameObject canvasObject = GameObject.Find(MainRecordingSettingsLayoutSpec.CanvasObjectName);
            Canvas canvas = canvasObject != null ? canvasObject.GetComponent<Canvas>() : null;
            if (canvas != null)
            {
                return canvas;
            }

            canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                return canvas;
            }

            var fallbackObject = new GameObject(MainRecordingSettingsLayoutSpec.CanvasObjectName, typeof(RectTransform));
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                fallbackObject.layer = uiLayer;
            }

            canvas = fallbackObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fallbackObject.AddComponent<CanvasScaler>();
            fallbackObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private void EnsureBuilt()
        {
            if (panelRoot != null)
            {
                return;
            }

            RectTransform root = EnsureRectTransform(gameObject);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = MainRecordingSettingsLayoutSpec.ReferenceSize;
            root.localScale = Vector3.one * MainRecordingSettingsLayoutSpec.DefaultDisplayScale;

            panelRoot = root;
            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (gameObject.GetComponent<RectMask2D>() == null)
            {
                gameObject.AddComponent<RectMask2D>();
            }
            CreateImage("Page", panelRoot, RectFull(), MainRecordingSettingsLayoutSpec.PageColor);
            BuildRail(panelRoot);
            BuildSidebar(panelRoot);
            BuildMainArea(panelRoot);
            SetVisible(false);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            EnsureBuilt();
            dragStartAnchoredPosition = panelRoot.anchoredPosition;
            dragStartPointerPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            EnsureBuilt();
            Vector2 pointerDelta = eventData.position - dragStartPointerPosition;
            panelRoot.anchoredPosition = dragStartAnchoredPosition + pointerDelta;
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void BuildRail(Transform parent)
        {
            CreateImage(
                "Rail",
                parent,
                new Rect(0f, 0f, MainRecordingSettingsLayoutSpec.RailWidth, MainRecordingSettingsLayoutSpec.ReferenceHeight),
                MainRecordingSettingsLayoutSpec.RailColor);
            CreateText("RailIconPrimary", parent, "□", 28, MainRecordingSettingsLayoutSpec.ActiveColor,
                FontStyles.Bold, TextAlignmentOptions.Center, new Rect(13f, 46f, 30f, 30f));
            CreateText("RailIconGraph", parent, "Σ", 28, new Color32(95, 108, 120, 255),
                FontStyles.Bold, TextAlignmentOptions.Center, new Rect(13f, 104f, 30f, 30f));
            CreateText("RailIconLight", parent, "!", 24, new Color32(95, 108, 120, 255),
                FontStyles.Bold, TextAlignmentOptions.Center, new Rect(13f, 160f, 30f, 30f));
            CreateImage("RailActiveMarker", parent, new Rect(50f, 34f, 4f, 40f),
                MainRecordingSettingsLayoutSpec.ActiveColor);
        }

        private void BuildSidebar(Transform parent)
        {
            CreateImage(
                "Sidebar",
                parent,
                new Rect(
                    MainRecordingSettingsLayoutSpec.SidebarX,
                    MainRecordingSettingsLayoutSpec.SidebarY,
                    MainRecordingSettingsLayoutSpec.SidebarWidth,
                    MainRecordingSettingsLayoutSpec.SidebarHeight),
                MainRecordingSettingsLayoutSpec.SidebarColor);

            CreateImage("SidebarHeader", parent, new Rect(62f, 39f, 237f, 35f),
                MainRecordingSettingsLayoutSpec.SidebarHeaderColor);
            CreateText("SidebarTitle", parent, MainRecordingSettingsLayoutSpec.WindowTitle, 16,
                MainRecordingSettingsLayoutSpec.ActiveColor, FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft, new Rect(101f, 46f, 178f, 22f));

            MainRecordingSettingsSidebarItemSpec[] sidebarItems = MainRecordingSettingsLayoutSpec.SidebarItems;

            CreateText("SidebarGroupCamera", parent, "시네마토그래피", 11, new Color32(52, 64, 76, 255),
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Rect(66f, 95f, 180f, 18f));
            CreateText("SidebarCamera", parent, sidebarItems[0].Label, 15, new Color32(36, 43, 51, 255),
                FontStyles.Normal, TextAlignmentOptions.MidlineLeft, new Rect(101f, 124f, 160f, 22f));
            CreateText("SidebarGroupEnvironment", parent, "환경", 11, new Color32(52, 64, 76, 255),
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Rect(66f, 172f, 180f, 18f));
            CreateText("SidebarEnvironment", parent, sidebarItems[1].Label, 15, new Color32(36, 43, 51, 255),
                FontStyles.Normal, TextAlignmentOptions.MidlineLeft, new Rect(101f, 199f, 170f, 22f));
            CreateText("SidebarLight", parent, sidebarItems[2].Label, 15, new Color32(36, 43, 51, 255),
                FontStyles.Normal, TextAlignmentOptions.MidlineLeft, new Rect(101f, 238f, 178f, 22f));

            CreateText("SidebarBottomTools", parent, "+  -  □  ✎  ⊞  ···", 25,
                MainRecordingSettingsLayoutSpec.ActiveColor, FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft, new Rect(72f, 631f, 190f, 34f));
        }

        private void BuildMainArea(Transform parent)
        {
            CreateText("MainTitle", parent, MainRecordingSettingsLayoutSpec.WindowTitle, 22,
                new Color32(52, 64, 76, 255), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft, new Rect(
                    MainRecordingSettingsLayoutSpec.MainX,
                    MainRecordingSettingsLayoutSpec.TitleY,
                    360f,
                    34f));

            Rect viewportRect = new Rect(305f, 31f, 950f, 644f);
            RectTransform viewport = CreateRectTransform("MainViewport", parent, viewportRect);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateRectTransform(
                "MainContent",
                viewport,
                new Rect(0f, 0f, viewportRect.width, 780f));

            cardButtons.Clear();
            MainRecordingSettingsCardSpec[] cards = MainRecordingSettingsLayoutSpec.Cards;
            for (int i = 0; i < cards.Length; i++)
            {
                float y = (MainRecordingSettingsLayoutSpec.CardY - viewportRect.y) +
                          (i * (MainRecordingSettingsLayoutSpec.CardHeight + MainRecordingSettingsLayoutSpec.CardGap));
                BuildCard(content, cards[i], new Rect(
                    MainRecordingSettingsLayoutSpec.CardX - viewportRect.x,
                    y,
                    MainRecordingSettingsLayoutSpec.CardWidth,
                    MainRecordingSettingsLayoutSpec.CardHeight));
            }

            CreateImage("StaticScrollbar", parent, new Rect(1257f, 34f, 4f, 406f),
                MainRecordingSettingsLayoutSpec.ActiveColor);
            Button closeButton = CreateButton(
                "CloseButton",
                parent,
                "닫기",
                new Rect(1190f, 632f, 56f, 32f),
                true);
            closeButton.onClick.AddListener(Close);
            notificationText = CreateText("Notification", parent, string.Empty, 13,
                new Color32(80, 88, 96, 255), FontStyles.Normal,
                TextAlignmentOptions.MidlineRight, new Rect(860f, 632f, 300f, 32f));
        }

        private void BuildCard(Transform parent, MainRecordingSettingsCardSpec card, Rect rect)
        {
            CreateImage(card.Title + " Card", parent, rect, card.BackgroundColor);
            CreateText(card.Title + " Title", parent, card.Title, 25, Color.white, FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft, new Rect(
                    rect.x + MainRecordingSettingsLayoutSpec.CardTextX,
                    rect.y + MainRecordingSettingsLayoutSpec.CardTitleY,
                    310f,
                    38f));
            CreateText(card.Title + " Body", parent, card.Body, 15, card.BodyTextColor, FontStyles.Bold,
                TextAlignmentOptions.TopLeft, new Rect(
                    rect.x + MainRecordingSettingsLayoutSpec.CardTextX,
                    rect.y + MainRecordingSettingsLayoutSpec.CardBodyY,
                    330f,
                    46f));

            Button button = CreateButton(
                card.Title + " Button",
                parent,
                card.ButtonLabel,
                new Rect(
                    rect.x + MainRecordingSettingsLayoutSpec.CardTextX,
                    rect.y + MainRecordingSettingsLayoutSpec.CardButtonY,
                    MainRecordingSettingsLayoutSpec.CardButtonWidth,
                    MainRecordingSettingsLayoutSpec.CardButtonHeight),
                card.Enabled);
            MainRecordingSettingsActionType action = card.Action;
            button.onClick.AddListener(() => HandleCardAction(action));
            cardButtons.Add(button);
        }

        private void HandleCardAction(MainRecordingSettingsActionType action)
        {
            if (action == MainRecordingSettingsActionType.Close)
            {
                Close();
                return;
            }

            MainRecordingSettingsActions.Execute(action, recodingSetting, fileManager, ShowNotification);
        }

        private void ShowNotification(string message)
        {
            EnsureBuilt();
            notificationText.text = message;
        }

        private Button CreateButton(string name, Transform parent, string label, Rect rect, bool interactable)
        {
            RectTransform buttonRect = CreateRectTransform(name, parent, rect);
            Image image = buttonRect.gameObject.AddComponent<Image>();
            image.color = interactable
                ? MainRecordingSettingsLayoutSpec.ButtonColor
                : MainRecordingSettingsLayoutSpec.DisabledButtonColor;
            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = interactable;

            Color textColor = interactable
                ? new Color32(36, 39, 44, 255)
                : MainRecordingSettingsLayoutSpec.DisabledButtonTextColor;
            CreateText(name + " Text", buttonRect, label, 16, textColor, FontStyles.Bold,
                TextAlignmentOptions.Center, new Rect(0f, 0f, rect.width, rect.height));
            return button;
        }

        private Image CreateImage(string name, Transform parent, Rect rect, Color color)
        {
            RectTransform imageRect = CreateRectTransform(name, parent, rect);
            Image image = imageRect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string text,
            int fontSize,
            Color color,
            FontStyles style,
            TextAlignmentOptions alignment,
            Rect rect)
        {
            RectTransform textRect = CreateRectTransform(name, parent, rect);
            TextMeshProUGUI label = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = alignment;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            ApplyReadableKoreanFont(label, text, fontSize, style, alignment);
            if (notificationText == null && name == "Notification")
            {
                notificationText = label;
            }

            return label;
        }

        private static void ApplyReadableKoreanFont(
            TextMeshProUGUI label,
            string text,
            int fontSize,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            if (label == null || !ContainsKorean(text) || FontAssetSupportsText(label.font, text))
            {
                return;
            }

            if (TryEnableLegacyKoreanText(label, text, fontSize, style, alignment))
            {
                return;
            }

            TMP_FontAsset koreanFont = GetOrCreateKoreanUiFont();
            if (koreanFont == null || !FontAssetSupportsText(koreanFont, text))
            {
                if (!warnedMissingKoreanUiFont)
                {
                    warnedMissingKoreanUiFont = true;
                    Debug.LogWarning("[MainRecordingSettingsPopup] 한글 UI 폰트를 찾지 못했습니다. OS 한글 폰트 설치 상태를 확인하세요.");
                }

                return;
            }

            label.font = koreanFont;
            label.SetAllDirty();
        }

        private static bool TryEnableLegacyKoreanText(
            TextMeshProUGUI label,
            string text,
            int fontSize,
            FontStyles style,
            TextAlignmentOptions alignment)
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
            fallbackText.text = text;
            fallbackText.font = legacyFont;
            fallbackText.fontSize = Mathf.Max(1, fontSize);
            fallbackText.fontStyle = (style & FontStyles.Bold) == FontStyles.Bold
                ? FontStyle.Bold
                : FontStyle.Normal;
            fallbackText.alignment = ConvertToLegacyAlignment(alignment);
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

            fontAsset.name = "Main Recording Runtime Korean UI Font";
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

        private static Rect RectFull()
        {
            return new Rect(
                0f,
                0f,
                MainRecordingSettingsLayoutSpec.ReferenceWidth,
                MainRecordingSettingsLayoutSpec.ReferenceHeight);
        }

        private RectTransform CreateRectTransform(string name, Transform parent, Rect rect)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.layer = gameObject.layer;
            RectTransform transform = child.GetComponent<RectTransform>();
            transform.SetParent(parent, false);
            transform.anchorMin = new Vector2(0f, 1f);
            transform.anchorMax = new Vector2(0f, 1f);
            transform.pivot = new Vector2(0f, 1f);
            transform.anchoredPosition = new Vector2(rect.x, -rect.y);
            transform.sizeDelta = new Vector2(rect.width, rect.height);
            transform.localScale = Vector3.one;
            return transform;
        }

        private static RectTransform EnsureRectTransform(GameObject target)
        {
            RectTransform rectTransform = target.GetComponent<RectTransform>();
            return rectTransform != null ? rectTransform : target.AddComponent<RectTransform>();
        }
    }
}
