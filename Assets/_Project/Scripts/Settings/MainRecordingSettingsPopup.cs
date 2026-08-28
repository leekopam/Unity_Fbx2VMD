using System.Collections.Generic;
using Fbx2Vmd.FBXImporter;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Fbx2Vmd.Settings
{
    [DisallowMultipleComponent]
    public sealed class MainRecordingSettingsPopup : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] private bool openOnStart = true;
        [FormerlySerializedAs("recodingSetting")]
        [SerializeField] private RecordingSetting recordingSetting;
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
        public RecordingSetting RecordingSetting => recordingSetting;
        public FBXVmdPipeline FBXVmdPipeline => MainRecordingSettingsActions.ResolveFBXVmdPipeline(recordingSetting, fileManager);

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
            return MainRecordingSettingsPopupSceneResolver.EnsurePopup(owner);
        }

        public void Bind(RecordingSetting owner, FBXVmdPipeline manager)
        {
            if (owner != null)
            {
                recordingSetting = owner;
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

        internal void EnsureBuilt()
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
            AttachGeneratedControls(
                closeButton,
                builtCardButtons,
                builtCardButtons.Length,
                shouldBindListeners: true);

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
            AttachGeneratedControls(
                closeButton,
                restoredCardButtons,
                cards.Length,
                shouldRestoreButtonListeners);

            generatedContentRoot = contentRoot;
            notificationText = restoredNotification;
            return true;
        }

        private void AttachGeneratedControls(
            Button closeButton,
            Button[] resolvedCardButtons,
            int cardCount,
            bool shouldBindListeners)
        {
            if (shouldBindListeners)
            {
                BindCloseButton(closeButton);
            }

            cardButtons.Clear();
            MainRecordingSettingsCardSpec[] cards = MainRecordingSettingsLayoutSpec.Cards;
            for (int i = 0; i < cardCount; i++)
            {
                Button button = resolvedCardButtons[i];
                if (shouldBindListeners)
                {
                    BindCardButton(button, cards[i].Action);
                }

                cardButtons.Add(button);
            }
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

            MainRecordingSettingsActions.Execute(action, recordingSetting, fileManager, ShowNotification);
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
