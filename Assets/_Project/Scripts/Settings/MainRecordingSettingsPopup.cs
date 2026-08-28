using System.Collections.Generic;
using Fbx2Vmd.FBXImporter;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fbx2Vmd.Settings
{
    [DisallowMultipleComponent]
    public sealed class MainRecordingSettingsPopup : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] private bool openOnStart = true;
        [SerializeField] private RecordingSetting recodingSetting;
        [SerializeField] private FBXVmdPipeline fileManager;

        private readonly List<Button> cardButtons = new List<Button>();
        private RectTransform panelRoot;
        private RectTransform generatedContentRoot;
        private CanvasGroup canvasGroup;
        private TextMeshProUGUI notificationText;
        private Vector2 dragStartAnchoredPosition;
        private Vector2 dragStartPointerPosition;
        private bool isOpen;
        public bool IsOpen => isOpen;
        public bool OpenOnStart => openOnStart;
        public RecordingSetting RecordingSetting => recodingSetting;
        public FBXVmdPipeline FBXVmdPipeline => MainRecordingSettingsActions.ResolveFBXVmdPipeline(recodingSetting, fileManager);

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

        public static MainRecordingSettingsPopup EnsurePopupForScene(RecordingSetting owner)
        {
            Canvas canvas = ResolveCanvas(owner);
            MainRecordingSettingsPopup popup = canvas.GetComponentInChildren<MainRecordingSettingsPopup>(true);
            if (popup == null)
            {
                var popupObject = new GameObject(MainRecordingSettingsLayoutSpec.PopupObjectName, typeof(RectTransform));
                popupObject.layer = canvas.gameObject.layer;
                popupObject.transform.SetParent(canvas.transform, false);
                popup = popupObject.AddComponent<MainRecordingSettingsPopup>();
            }

            popup.Bind(owner, MainRecordingSettingsActions.ResolveFBXVmdPipeline(owner));
            popup.EnsureBuilt();
            return popup;
        }

        public void Bind(RecordingSetting owner, FBXVmdPipeline manager)
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
                if (!KoreanUiTextFallback.IsReadable(label))
                {
                    return false;
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

        private static Canvas ResolveCanvas(RecordingSetting owner)
        {
            Canvas canvas = owner != null ? owner.GetComponentInParent<Canvas>() : null;
            if (canvas != null)
            {
                return canvas;
            }

            GameObject canvasObject = GameObject.Find(MainRecordingSettingsLayoutSpec.CanvasObjectName);
            canvas = canvasObject != null ? canvasObject.GetComponent<Canvas>() : null;
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
            RectTransform ownedContentRoot = null;
            bool hasSingleOwnedContent = panelRoot != null &&
                MainRecordingSettingsGeneratedHierarchy.TryFindContent(
                    panelRoot,
                    out ownedContentRoot);
            if (hasSingleOwnedContent &&
                generatedContentRoot != null &&
                generatedContentRoot == ownedContentRoot &&
                MainRecordingSettingsGeneratedHierarchy.HasCompleteContent(
                    generatedContentRoot,
                    MainRecordingSettingsLayoutSpec.Cards) &&
                canvasGroup != null &&
                notificationText != null &&
                cardButtons.Count == MainRecordingSettingsLayoutSpec.Cards.Length)
            {
                return;
            }

            RectTransform root = EnsureRectTransform(gameObject);
            panelRoot = root;
            bool hadGeneratedHierarchy = MainRecordingSettingsGeneratedHierarchy.HasOwnedRootChildren(panelRoot);
            bool hadCanvasGroup = gameObject.GetComponent<CanvasGroup>() != null;
            string preservedNotification = hadGeneratedHierarchy
                ? MainRecordingSettingsGeneratedHierarchy.FindNotificationText(panelRoot)
                : string.Empty;
            int generatedContentSiblingIndex =
                MainRecordingSettingsGeneratedHierarchy.FindOwnedSiblingIndex(panelRoot);
            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (gameObject.GetComponent<RectMask2D>() == null)
            {
                gameObject.AddComponent<RectMask2D>();
            }

            ConfigurePanelRoot(root, !hadGeneratedHierarchy);
            if (TryRestoreGeneratedHierarchy())
            {
                return;
            }

            var elementBuilder = new MainRecordingSettingsElementBuilder(gameObject.layer);
            if (TryMigrateLegacyGeneratedHierarchy(elementBuilder))
            {
                return;
            }

            generatedContentSiblingIndex =
                MainRecordingSettingsGeneratedHierarchy.FindOwnedSiblingIndex(panelRoot);
            RemoveOwnedGeneratedHierarchy();
            generatedContentRoot = elementBuilder.CreateRectTransform(
                MainRecordingSettingsGeneratedHierarchy.ContentObjectName,
                panelRoot,
                RectFull());
            MainRecordingSettingsGeneratedHierarchy.MarkOwned(generatedContentRoot);
            MainRecordingSettingsGeneratedHierarchy.RestoreSiblingIndex(
                generatedContentRoot,
                generatedContentSiblingIndex);
            Button[] builtCardButtons = MainRecordingSettingsPopupViewBuilder.Build(
                generatedContentRoot,
                elementBuilder,
                out Button closeButton,
                out notificationText);
            cardButtons.Clear();
            MainRecordingSettingsCardSpec[] cards = MainRecordingSettingsLayoutSpec.Cards;
            for (int i = 0; i < builtCardButtons.Length; i++)
            {
                Button button = builtCardButtons[i];
                BindCardButton(button, cards[i].Action);
                cardButtons.Add(button);
            }

            BindCloseButton(closeButton);

            if (hadGeneratedHierarchy)
            {
                if (!string.IsNullOrEmpty(preservedNotification))
                {
                    notificationText.text = preservedNotification;
                    KoreanUiTextFallback.Apply(notificationText);
                }

                if (!hadCanvasGroup)
                {
                    SetVisible(isOpen);
                }
            }
            else
            {
                SetVisible(false);
            }
        }

        private bool TryRestoreGeneratedHierarchy()
        {
            if (!MainRecordingSettingsGeneratedHierarchy.TryFindContent(
                    panelRoot,
                    out RectTransform contentRoot))
            {
                return false;
            }

            return TryRestoreGeneratedHierarchy(contentRoot);
        }

        private bool TryRestoreGeneratedHierarchy(RectTransform contentRoot)
        {
            MainRecordingSettingsCardSpec[] cards = MainRecordingSettingsLayoutSpec.Cards;
            if (!MainRecordingSettingsGeneratedHierarchy.TryResolveControls(
                    contentRoot,
                    cards,
                    out Button closeButton,
                    out TextMeshProUGUI restoredNotification,
                    out Button[] restoredCardButtons))
            {
                return false;
            }

            bool shouldRestoreButtonListeners = cardButtons.Count == 0;
            if (shouldRestoreButtonListeners)
            {
                BindCloseButton(closeButton);
            }

            cardButtons.Clear();
            for (int i = 0; i < cards.Length; i++)
            {
                Button button = restoredCardButtons[i];
                if (shouldRestoreButtonListeners)
                {
                    BindCardButton(button, cards[i].Action);
                }

                cardButtons.Add(button);
            }

            generatedContentRoot = contentRoot;
            notificationText = restoredNotification;
            return true;
        }

        private bool TryMigrateLegacyGeneratedHierarchy(
            MainRecordingSettingsElementBuilder elementBuilder)
        {
            var legacyChildren = new List<Transform>(
                MainRecordingSettingsGeneratedHierarchy.ContentChildCount);
            if (!MainRecordingSettingsGeneratedHierarchy.TryCollectLegacyRootChildren(
                    panelRoot,
                    legacyChildren))
            {
                return false;
            }

            int legacySiblingIndex = legacyChildren[0].GetSiblingIndex();
            RectTransform migratedContent = elementBuilder.CreateRectTransform(
                MainRecordingSettingsGeneratedHierarchy.ContentObjectName,
                panelRoot,
                RectFull());
            MainRecordingSettingsGeneratedHierarchy.MarkOwned(migratedContent);
            MainRecordingSettingsGeneratedHierarchy.MoveChildren(legacyChildren, migratedContent);
            MainRecordingSettingsGeneratedHierarchy.RestoreSiblingIndex(
                migratedContent,
                legacySiblingIndex);

            return TryRestoreGeneratedHierarchy(migratedContent);
        }

        private void RemoveOwnedGeneratedHierarchy()
        {
            generatedContentRoot = null;
            notificationText = null;
            cardButtons.Clear();
            MainRecordingSettingsGeneratedHierarchy.RemoveOwnedRootChildren(panelRoot);
        }

        private static void ConfigurePanelRoot(RectTransform root, bool resetAnchoredPosition)
        {
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            if (resetAnchoredPosition)
            {
                root.anchoredPosition = Vector2.zero;
            }

            root.sizeDelta = MainRecordingSettingsLayoutSpec.ReferenceSize;
            root.localScale = Vector3.one * MainRecordingSettingsLayoutSpec.DefaultDisplayScale;
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

        private void BindCloseButton(Button button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(Close);
        }

        private void BindCardButton(Button button, MainRecordingSettingsActionType action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => HandleCardAction(action));
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
            KoreanUiTextFallback.Apply(notificationText);
        }

        private static Rect RectFull()
        {
            return new Rect(
                0f,
                0f,
                MainRecordingSettingsLayoutSpec.ReferenceWidth,
                MainRecordingSettingsLayoutSpec.ReferenceHeight);
        }

        private static RectTransform EnsureRectTransform(GameObject target)
        {
            RectTransform rectTransform = target.GetComponent<RectTransform>();
            return rectTransform != null ? rectTransform : target.AddComponent<RectTransform>();
        }
    }
}
