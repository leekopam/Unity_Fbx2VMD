using System;
using System.Collections.Generic;
using System.Reflection;
using Member_Han.Modules.FBXImporter;
using Member_Han.Modules.Graphics;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Member_Han.Modules.Graphics.EditorTools
{
    public readonly struct MainRecordingSettingsWindowContext
    {
        public MainRecordingSettingsWindowContext(
            GameObject settingRoot,
            GraphicSetting graphicSetting,
            BackgroundColorSetting backgroundColorSetting,
            RecodingSetting recodingSetting,
            Camera targetCamera)
        {
            SettingRoot = settingRoot;
            GraphicSetting = graphicSetting;
            BackgroundColorSetting = backgroundColorSetting;
            RecodingSetting = recodingSetting;
            TargetCamera = targetCamera;
        }

        public GameObject SettingRoot { get; }
        public GraphicSetting GraphicSetting { get; }
        public BackgroundColorSetting BackgroundColorSetting { get; }
        public RecodingSetting RecodingSetting { get; }
        public Camera TargetCamera { get; }
        public bool IsComplete => SettingRoot != null &&
                                  GraphicSetting != null &&
                                  BackgroundColorSetting != null &&
                                  RecodingSetting != null &&
                                  TargetCamera != null;
    }

    public sealed class MainRecordingSettingsWindow : EditorWindow
    {
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_Recoding.unity";
        private const string PreviewActionLabel = "Preview";
        internal const string BurstCleanCompileLogMessage =
            "[MainRecordingSettingsWindow] Main_Recoding Play 준비를 위해 Burst direct-call 컴파일을 비활성화하고 스크립트 clean compile을 요청했습니다.";
        private static readonly Dictionary<string, Texture2D> RoundedTextureCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> GuiPackTextureCache = new Dictionary<string, Texture2D>();

        private static GUIStyle titleStyle;
        private static GUIStyle sidebarHeaderStyle;
        private static GUIStyle sidebarItemStyle;
        private static GUIStyle sidebarInactiveItemStyle;
        private static GUIStyle cardTitleStyle;
        private static GUIStyle cardBodyStyle;
        private static GUIStyle cardButtonStyle;
        private static GUIStyle iconStyle;
        private static GUIStyle toolbarStyle;
        private Vector2 onboardingCardsScrollPosition;
        private static Font koreanUiFont;
        private static bool warnedMissingKoreanUiFont;
        private static readonly string[] KoreanUiFontNames =
        {
            "Malgun Gothic",
            "맑은 고딕",
            "Noto Sans KR",
            "Noto Sans CJK KR",
            "NanumGothic",
            "Nanum Gothic",
        };

        [MenuItem("Tools/Graphics/Open Main_recording Settings")]
        public static MainRecordingSettingsWindow OpenForMainRecordingScene()
        {
            CloseExistingSettingsWindows();
            var window = CreateInstance<MainRecordingSettingsWindow>();
            Rect mainEditorPosition = EditorGUIUtility.GetMainWindowPosition();
            Rect floatingPosition = GetDefaultFloatingWindowPosition(mainEditorPosition);
            ConfigureFloatingWindow(window, mainEditorPosition);
            window.ShowUtility();
            window.position = floatingPosition;
            window.Focus();
            return window;
        }

        public static string GetWindowTitle()
        {
            return MainRecordingSettingsLayoutSpec.WindowTitle;
        }

        private static string GetMainRecordingScenePathForTests()
        {
            return MainRecordingScenePath;
        }

        public static bool ShouldOpenForScene(string scenePath)
        {
            return string.Equals(NormalizeScenePath(scenePath), MainRecordingScenePath, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldAutoOpenForPlayMode(
            string scenePath,
            bool isBatchMode,
            PlayModeStateChange playModeState)
        {
            return playModeState == PlayModeStateChange.EnteredPlayMode &&
                   !isBatchMode &&
                   ShouldOpenForScene(scenePath);
        }

        public static MainRecordingSettingsWindowContext ResolveContext()
        {
            GameObject root = GameObject.Find("Setting");
            GraphicSetting graphicSetting = root != null ? root.GetComponent<GraphicSetting>() : FindObjectOfType<GraphicSetting>();
            if (root == null && graphicSetting != null)
            {
                root = graphicSetting.gameObject;
            }

            BackgroundColorSetting backgroundColorSetting =
                root != null ? root.GetComponent<BackgroundColorSetting>() : FindObjectOfType<BackgroundColorSetting>();
            RecodingSetting recodingSetting =
                root != null ? root.GetComponent<RecodingSetting>() : FindObjectOfType<RecodingSetting>();
            Camera targetCamera = backgroundColorSetting != null && backgroundColorSetting.TargetCamera != null
                ? backgroundColorSetting.TargetCamera
                : Camera.main;

            return new MainRecordingSettingsWindowContext(
                root,
                graphicSetting,
                backgroundColorSetting,
                recodingSetting,
                targetCamera);
        }

        private static string[] GetSidebarItemLabelsForTests()
        {
            MainRecordingSettingsSidebarItemSpec[] items = MainRecordingSettingsLayoutSpec.SidebarItems;
            var labels = new string[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                labels[i] = items[i].Label;
            }

            return labels;
        }

        private static string[] GetOnboardingCardTitlesForTests()
        {
            MainRecordingSettingsCardSpec[] cards = MainRecordingSettingsLayoutSpec.Cards;
            var titles = new string[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                titles[i] = cards[i].Title;
            }

            return titles;
        }

        private static string[] GetOnboardingCardButtonLabelsForTests()
        {
            MainRecordingSettingsCardSpec[] cards = MainRecordingSettingsLayoutSpec.Cards;
            var labels = new string[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                labels[i] = cards[i].ButtonLabel;
            }

            return labels;
        }

        private static float GetCardButtonPreferredWidthForTests(string label)
        {
            EnsureStyles();
            return cardButtonStyle.CalcSize(new GUIContent(label ?? string.Empty)).x;
        }

        private static float GetCardButtonAvailableWidthForTests()
        {
            return MainRecordingSettingsLayoutSpec.CardButtonWidth;
        }

        private static string GetCardButtonClippingForTests()
        {
            EnsureStyles();
            return cardButtonStyle.clipping.ToString();
        }

        private static string[] GetOnboardingCardBodiesForTests()
        {
            MainRecordingSettingsCardSpec[] cards = MainRecordingSettingsLayoutSpec.Cards;
            var bodies = new string[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                bodies[i] = cards[i].Body;
            }

            return bodies;
        }

        private static string[] GetOnboardingCardActionsForTests()
        {
            MainRecordingSettingsCardSpec[] cards = MainRecordingSettingsLayoutSpec.Cards;
            var actions = new string[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                actions[i] = cards[i].Action.ToString();
            }

            return actions;
        }

        private static bool[] GetOnboardingCardEnabledStatesForTests()
        {
            MainRecordingSettingsCardSpec[] cards = MainRecordingSettingsLayoutSpec.Cards;
            var states = new bool[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                states[i] = cards[i].Enabled;
            }

            return states;
        }

        private static bool CanHandleImportFbxActionForTests()
        {
            MainRecordingSettingsWindowContext context = ResolveContext();
            return MainRecordingSettingsActions.CanExecute(
                MainRecordingSettingsActionType.ImportFbx,
                context.RecodingSetting);
        }

        private static string GetPreviewActionLabelForTests()
        {
            return PreviewActionLabel;
        }

        private static bool CanOpenRecordingPreviewActionForTests()
        {
            return !string.IsNullOrEmpty(RecordingPreviewWindow.MenuPath) &&
                   string.Equals(RecordingPreviewWindow.GetWindowTitle(), "Recording Preview", StringComparison.Ordinal);
        }

        private static string GetVisualAssetPolicyForTests()
        {
            return MainRecordingSettingsLayoutSpec.VisualAssetPolicy;
        }

        private static string GetEditorSurfacePolicyForTests()
        {
            return MainRecordingSettingsLayoutSpec.EditorSurfacePolicy;
        }

        private static string[] GetRequiredGuiPackAssetPathsForTests()
        {
            string[] paths = MainRecordingSettingsLayoutSpec.RequiredGuiPackAssetPaths;
            var copy = new string[paths.Length];
            Array.Copy(paths, copy, paths.Length);
            return copy;
        }

        private static int CountRequiredGuiPackAssetsAvailableForTests()
        {
            int count = 0;
            foreach (string assetPath in MainRecordingSettingsLayoutSpec.RequiredGuiPackAssetPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool HasReadableKoreanTextForTests()
        {
            foreach (string text in MainRecordingSettingsLayoutSpec.KoreanUiTextSamples)
            {
                if (string.IsNullOrEmpty(text) ||
                    !ContainsKorean(text) ||
                    ContainsBrokenTextMarker(text))
                {
                    return false;
                }
            }

            return GetOrCreateKoreanUiFont() != null;
        }

        private static string[] GetVisibleSettingsWindowTextForTests()
        {
            MainRecordingSettingsSidebarItemSpec[] sidebarItems = MainRecordingSettingsLayoutSpec.SidebarItems;
            MainRecordingSettingsCardSpec[] cards = MainRecordingSettingsLayoutSpec.Cards;
            var texts = new List<string>
            {
                MainRecordingSettingsLayoutSpec.WindowTitle,
                "시네마토그래피",
                "환경",
                "Camera 1",
                "Environment",
                "Directional Light",
                "+  -  □  ✎  ⊞  ···",
            };

            for (int i = 0; i < sidebarItems.Length; i++)
            {
                texts.Add(sidebarItems[i].Label);
            }

            for (int i = 0; i < cards.Length; i++)
            {
                texts.Add(cards[i].Title);
                texts.Add(cards[i].Body);
                texts.Add(cards[i].ButtonLabel);
            }

            return texts.ToArray();
        }

        private static Rect GetOnboardingCardsViewportRectForTests()
        {
            return GetOnboardingCardsViewportRect();
        }

        private static bool HasSharedSettingsFooterForTests()
        {
            return false;
        }

        private static float GetOnboardingCardsContentHeightForTests()
        {
            return GetOnboardingCardsContentHeight();
        }

        private static bool ShouldAutoOpenEditorWindowForPlayModeForTests(
            string scenePath,
            bool isBatchMode,
            PlayModeStateChange playModeState)
        {
            return ShouldAutoOpenForPlayMode(scenePath, isBatchMode, playModeState);
        }

        private static Vector2 GetReferenceWindowSizeForTests()
        {
            return MainRecordingSettingsLayoutSpec.ReferenceSize;
        }

        private static float GetDefaultDisplayScaleForTests()
        {
            return MainRecordingSettingsLayoutSpec.DefaultDisplayScale;
        }

        private static Vector2 GetDefaultDisplayWindowSizeForTests()
        {
            return MainRecordingSettingsLayoutSpec.DefaultDisplaySize;
        }

        private static string GetWindowPresentationModeForTests()
        {
            return "utility-floating";
        }

        private static Rect GetDefaultFloatingWindowPositionForTests(Rect mainEditorPosition)
        {
            return GetDefaultFloatingWindowPosition(mainEditorPosition);
        }

        private static Vector2 GetReferenceCardSizeForTests()
        {
            return new Vector2(
                MainRecordingSettingsLayoutSpec.CardWidth,
                MainRecordingSettingsLayoutSpec.CardHeight);
        }

        private static bool UsesRuntimeSharedLayoutSpecForTests()
        {
            return MainRecordingSettingsLayoutSpec.Cards.Length == 3 &&
                   MainRecordingSettingsLayoutSpec.ReferenceWidth == 1265 &&
                   MainRecordingSettingsLayoutSpec.ReferenceHeight == 675;
        }

        private static string NormalizeScenePath(string scenePath)
        {
            return string.IsNullOrWhiteSpace(scenePath)
                ? string.Empty
                : scenePath.Replace('\\', '/').Trim();
        }

        private static void CloseExistingSettingsWindows()
        {
            MainRecordingSettingsWindow[] windows = Resources.FindObjectsOfTypeAll<MainRecordingSettingsWindow>();
            foreach (MainRecordingSettingsWindow existingWindow in windows)
            {
                if (existingWindow != null)
                {
                    existingWindow.Close();
                }
            }
        }

        private static void ConfigureFloatingWindow(MainRecordingSettingsWindow window, Rect mainEditorPosition)
        {
            window.titleContent = new GUIContent(MainRecordingSettingsLayoutSpec.WindowTitle);
            window.minSize = MainRecordingSettingsLayoutSpec.ReferenceSize;
            window.maxSize = MainRecordingSettingsLayoutSpec.MaximumDisplaySize;
            window.position = GetDefaultFloatingWindowPosition(mainEditorPosition);
        }

        private static Rect GetDefaultFloatingWindowPosition(Rect mainEditorPosition)
        {
            Vector2 size = MainRecordingSettingsLayoutSpec.DefaultDisplaySize;
            if (mainEditorPosition.width <= 0f || mainEditorPosition.height <= 0f)
            {
                return new Rect(96f, 96f, size.x, size.y);
            }

            float x = mainEditorPosition.x + Mathf.Max(64f, (mainEditorPosition.width - size.x) * 0.5f);
            float y = mainEditorPosition.y + Mathf.Max(72f, (mainEditorPosition.height - size.y) * 0.5f);
            return new Rect(x, y, size.x, size.y);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(MainRecordingSettingsLayoutSpec.WindowTitle);
            minSize = MainRecordingSettingsLayoutSpec.ReferenceSize;
            maxSize = MainRecordingSettingsLayoutSpec.MaximumDisplaySize;
        }

        private void OnGUI()
        {
            EnsureStyles();

            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height),
                MainRecordingSettingsLayoutSpec.PageColor);

            Matrix4x4 previousMatrix = GUI.matrix;
            try
            {
                float displayScale = GetContentDisplayScale(position.size);
                GUIUtility.ScaleAroundPivot(Vector2.one * displayScale, Vector2.zero);

                Rect root = new Rect(
                    0f,
                    0f,
                    MainRecordingSettingsLayoutSpec.ReferenceWidth,
                    MainRecordingSettingsLayoutSpec.ReferenceHeight);
                DrawRail(root.height);
                DrawSidebar();
                DrawMainArea(root);
                DrawStaticScrollbar();
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }

        private static float GetContentDisplayScale(Vector2 windowSize)
        {
            float widthScale = windowSize.x / MainRecordingSettingsLayoutSpec.ReferenceWidth;
            float heightScale = windowSize.y / MainRecordingSettingsLayoutSpec.ReferenceHeight;
            return Mathf.Clamp(
                Mathf.Min(widthScale, heightScale),
                1f,
                MainRecordingSettingsLayoutSpec.MaximumDisplayScale);
        }

        private static void DrawRail(float height)
        {
            DrawRect(new Rect(0f, 0f, MainRecordingSettingsLayoutSpec.RailWidth, height),
                MainRecordingSettingsLayoutSpec.RailColor);
            DrawRect(new Rect(MainRecordingSettingsLayoutSpec.RailWidth - 1f, 0f, 1f, height),
                new Color32(224, 231, 239, 255));
            DrawGuiPackIcon(new Rect(13f, 46f, 30f, 30f), MainRecordingSettingsLayoutSpec.GuiPackHomeIconPath, "□");
            DrawGuiPackIcon(new Rect(13f, 104f, 30f, 30f), MainRecordingSettingsLayoutSpec.GuiPackSettingsIconPath, "Σ");
            DrawGuiPackIcon(new Rect(13f, 160f, 30f, 30f), MainRecordingSettingsLayoutSpec.GuiPackInfoIconPath, "!");
            DrawRect(new Rect(50f, 34f, 4f, 40f), MainRecordingSettingsLayoutSpec.ActiveColor);
            DrawGuiPackIcon(new Rect(14f, height - 88f, 30f, 30f), MainRecordingSettingsLayoutSpec.GuiPackVideoIconPath, "TV");
            DrawGuiPackIcon(new Rect(14f, height - 42f, 30f, 30f), MainRecordingSettingsLayoutSpec.GuiPackMenuIconPath, "...");
        }

        private static void DrawSidebar()
        {
            Rect sidebarRect = new Rect(
                MainRecordingSettingsLayoutSpec.SidebarX,
                MainRecordingSettingsLayoutSpec.SidebarY,
                MainRecordingSettingsLayoutSpec.SidebarWidth,
                MainRecordingSettingsLayoutSpec.SidebarHeight);
            DrawRect(sidebarRect, MainRecordingSettingsLayoutSpec.SidebarColor);
            DrawRect(new Rect(62f, 39f, 237f, 35f), MainRecordingSettingsLayoutSpec.SidebarHeaderColor);
            GUI.Label(new Rect(101f, 46f, 178f, 22f), MainRecordingSettingsLayoutSpec.WindowTitle, sidebarHeaderStyle);

            GUI.Label(new Rect(66f, 95f, 180f, 18f), "시네마토그래피", sidebarInactiveItemStyle);
            DrawGuiPackIcon(new Rect(72f, 121f, 22f, 22f), MainRecordingSettingsLayoutSpec.GuiPackVideoIconPath, "C");
            GUI.Label(new Rect(101f, 124f, 160f, 22f), "Camera 1", sidebarItemStyle);
            GUI.Label(new Rect(66f, 172f, 180f, 18f), "환경", sidebarInactiveItemStyle);
            DrawGuiPackIcon(new Rect(72f, 196f, 22f, 22f), MainRecordingSettingsLayoutSpec.GuiPackSettingsIconPath, "E");
            GUI.Label(new Rect(101f, 199f, 170f, 22f), "Environment", sidebarItemStyle);
            DrawGuiPackIcon(new Rect(72f, 235f, 22f, 22f), MainRecordingSettingsLayoutSpec.GuiPackInfoIconPath, "L");
            GUI.Label(new Rect(101f, 238f, 178f, 22f), "Directional Light", sidebarItemStyle);

            GUI.Label(new Rect(72f, 631f, 190f, 34f), "+  -  □  ✎  ⊞  ···", toolbarStyle);
        }

        private void DrawMainArea(Rect root)
        {
            GUI.Label(
                new Rect(
                    MainRecordingSettingsLayoutSpec.MainX,
                    MainRecordingSettingsLayoutSpec.TitleY,
                    360f,
                    34f),
                MainRecordingSettingsLayoutSpec.WindowTitle,
                titleStyle);

            Rect viewport = GetOnboardingCardsViewportRect();
            Rect contentRect = new Rect(0f, 0f, viewport.width - 18f, GetOnboardingCardsContentHeight());
            onboardingCardsScrollPosition = GUI.BeginScrollView(
                viewport,
                onboardingCardsScrollPosition,
                contentRect,
                false,
                true);
            try
            {
                MainRecordingSettingsCardSpec[] cards = MainRecordingSettingsLayoutSpec.Cards;
                for (int i = 0; i < cards.Length; i++)
                {
                    float y = (MainRecordingSettingsLayoutSpec.CardY - viewport.y) +
                              (i * (MainRecordingSettingsLayoutSpec.CardHeight + MainRecordingSettingsLayoutSpec.CardGap));
                    DrawCard(
                        new Rect(
                            MainRecordingSettingsLayoutSpec.CardX - viewport.x,
                            y,
                            MainRecordingSettingsLayoutSpec.CardWidth,
                            MainRecordingSettingsLayoutSpec.CardHeight),
                        cards[i]);
                }
            }
            finally
            {
                GUI.EndScrollView();
            }
        }

        private void DrawCard(Rect rect, MainRecordingSettingsCardSpec card)
        {
            DrawRoundedRect(rect, card.BackgroundColor, MainRecordingSettingsLayoutSpec.CardCornerRadius);
            GUI.Label(
                new Rect(
                    rect.x + MainRecordingSettingsLayoutSpec.CardTextX,
                    rect.y + MainRecordingSettingsLayoutSpec.CardTitleY,
                    310f,
                    38f),
                card.Title,
                cardTitleStyle);

            Color oldColor = GUI.color;
            GUI.color = card.BodyTextColor;
            GUI.Label(
                new Rect(
                    rect.x + MainRecordingSettingsLayoutSpec.CardTextX,
                    rect.y + MainRecordingSettingsLayoutSpec.CardBodyY,
                    330f,
                    46f),
                card.Body,
                cardBodyStyle);
            GUI.color = oldColor;

            Rect buttonRect = new Rect(
                rect.x + MainRecordingSettingsLayoutSpec.CardTextX,
                rect.y + MainRecordingSettingsLayoutSpec.CardButtonY,
                MainRecordingSettingsLayoutSpec.CardButtonWidth,
                MainRecordingSettingsLayoutSpec.CardButtonHeight);
            using (new EditorGUI.DisabledScope(!card.Enabled))
            {
                if (GUI.Button(buttonRect, card.ButtonLabel, cardButtonStyle))
                {
                    HandleCardAction(card.Action);
                }
            }
        }

        private void HandleCardAction(MainRecordingSettingsActionType action)
        {
            if (action == MainRecordingSettingsActionType.Close)
            {
                Close();
                return;
            }

            MainRecordingSettingsWindowContext context = ResolveContext();
            MainRecordingSettingsActions.Execute(action, context.RecodingSetting, null, message =>
            {
                ShowNotification(new GUIContent(message));
            });
        }

        private static Rect GetOnboardingCardsViewportRect()
        {
            return new Rect(
                305f,
                31f,
                950f,
                MainRecordingSettingsLayoutSpec.ReferenceHeight - 31f);
        }

        private static float GetOnboardingCardsContentHeight()
        {
            int cardCount = MainRecordingSettingsLayoutSpec.Cards.Length;
            if (cardCount <= 0)
            {
                return 0f;
            }

            float firstCardY = MainRecordingSettingsLayoutSpec.CardY - GetOnboardingCardsViewportRect().y;
            return firstCardY +
                   (cardCount * MainRecordingSettingsLayoutSpec.CardHeight) +
                   ((cardCount - 1) * MainRecordingSettingsLayoutSpec.CardGap) +
                   24f;
        }

        private static void DrawStaticScrollbar()
        {
            DrawRect(new Rect(1257f, 34f, 4f, 406f), MainRecordingSettingsLayoutSpec.ActiveColor);
        }

        private static void EnsureStyles()
        {
            if (HasCompleteStyleCache())
            {
                return;
            }

            titleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
            };
            titleStyle.normal.textColor = new Color32(52, 64, 76, 255);

            sidebarHeaderStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
            };
            sidebarHeaderStyle.normal.textColor = MainRecordingSettingsLayoutSpec.ActiveColor;

            sidebarItemStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
            };
            sidebarItemStyle.normal.textColor = new Color32(36, 43, 51, 255);

            sidebarInactiveItemStyle = new GUIStyle(sidebarItemStyle)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
            };
            sidebarInactiveItemStyle.normal.textColor = MainRecordingSettingsLayoutSpec.MutedTextColor;

            cardTitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
            };
            cardTitleStyle.normal.textColor = Color.white;

            cardBodyStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                clipping = TextClipping.Clip,
            };
            cardBodyStyle.normal.textColor = Color.white;

            cardButtonStyle = new GUIStyle(GetCardButtonBaseStyle())
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                border = new RectOffset(8, 8, 8, 8),
                padding = new RectOffset(8, 8, 4, 4),
            };
            Texture2D buttonTexture = GetRoundedTexture(MainRecordingSettingsLayoutSpec.ButtonColor, 8f);
            cardButtonStyle.normal.background = buttonTexture;
            cardButtonStyle.hover.background = buttonTexture;
            cardButtonStyle.active.background = buttonTexture;
            cardButtonStyle.normal.textColor = new Color32(36, 39, 44, 255);
            cardButtonStyle.hover.textColor = new Color32(20, 24, 28, 255);
            cardButtonStyle.active.textColor = MainRecordingSettingsLayoutSpec.ActiveColor;

            iconStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
            };
            iconStyle.normal.textColor = MainRecordingSettingsLayoutSpec.ActiveColor;

            toolbarStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
            };
            toolbarStyle.normal.textColor = MainRecordingSettingsLayoutSpec.ActiveColor;

            ApplyKoreanFontFallback(titleStyle);
            ApplyKoreanFontFallback(sidebarHeaderStyle);
            ApplyKoreanFontFallback(sidebarItemStyle);
            ApplyKoreanFontFallback(sidebarInactiveItemStyle);
            ApplyKoreanFontFallback(cardTitleStyle);
            ApplyKoreanFontFallback(cardBodyStyle);
            ApplyKoreanFontFallback(cardButtonStyle);
            ApplyKoreanFontFallback(iconStyle);
            ApplyKoreanFontFallback(toolbarStyle);
        }

        private static GUIStyle GetCardButtonBaseStyle()
        {
            if (Event.current != null && GUI.skin != null && GUI.skin.button != null)
            {
                return GUI.skin.button;
            }

            return EditorStyles.miniButton ?? EditorStyles.label ?? GUIStyle.none;
        }

        private static bool HasCompleteStyleCache()
        {
            return titleStyle != null &&
                   sidebarHeaderStyle != null &&
                   sidebarItemStyle != null &&
                   sidebarInactiveItemStyle != null &&
                   cardTitleStyle != null &&
                   cardBodyStyle != null &&
                   cardButtonStyle != null &&
                   iconStyle != null &&
                   toolbarStyle != null;
        }

        private static void DrawRect(Rect rect, Color color)
        {
            EditorGUI.DrawRect(rect, color);
        }

        private static void DrawRoundedRect(Rect rect, Color color, float radius)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            GUI.DrawTexture(rect, GetRoundedTexture(color, radius, rect.width, rect.height), ScaleMode.StretchToFill, true);
        }

        private static Texture2D GetRoundedTexture(Color color, float radius)
        {
            return GetRoundedTexture(color, radius, 64f, 64f);
        }

        private static Texture2D GetRoundedTexture(Color color, float radius, float rectWidth, float rectHeight)
        {
            Color32 c = color;
            int width = Mathf.Max(1, Mathf.CeilToInt(rectWidth));
            int height = Mathf.Max(1, Mathf.CeilToInt(rectHeight));
            string key = c.r + "_" + c.g + "_" + c.b + "_" + c.a + "_" +
                         Mathf.RoundToInt(radius) + "_" + width + "x" + height;
            Texture2D texture;
            if (RoundedTextureCache.TryGetValue(key, out texture) && texture != null)
            {
                return texture;
            }

            texture = CreateRoundedTexture(color, radius, width, height);
            RoundedTextureCache[key] = texture;
            return texture;
        }

        private static Texture2D CreateRoundedTexture(Color color, float radius, int width, int height)
        {
            float r = Mathf.Clamp(radius, 0f, (Mathf.Min(width, height) - 1f) * 0.5f);
            var texture = new Texture2D(width, height, TextureFormat.ARGB32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float cx = Mathf.Clamp(px, r, width - r);
                    float cy = Mathf.Clamp(py, r, height - r);
                    float dx = px - cx;
                    float dy = py - cy;
                    bool outsideCorner = (dx * dx) + (dy * dy) > r * r;
                    pixels[(y * width) + x] = outsideCorner ? new Color(0f, 0f, 0f, 0f) : color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void DrawGuiPackIcon(Rect rect, string assetPath, string fallbackText)
        {
            Texture2D texture = LoadGuiPackTexture(assetPath);
            if (texture == null)
            {
                GUI.Label(rect, fallbackText, iconStyle);
                return;
            }

            Color previousColor = GUI.color;
            GUI.color = MainRecordingSettingsLayoutSpec.ActiveColor;
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            GUI.color = previousColor;
        }

        private static Texture2D LoadGuiPackTexture(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            Texture2D texture;
            if (GuiPackTextureCache.TryGetValue(assetPath, out texture))
            {
                return texture;
            }

            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            GuiPackTextureCache[assetPath] = texture;
            return texture;
        }

        private static void ApplyKoreanFontFallback(GUIStyle style)
        {
            if (style == null)
            {
                return;
            }

            Font font = GetOrCreateKoreanUiFont();
            if (font != null)
            {
                style.font = font;
            }
        }

        private static Font GetOrCreateKoreanUiFont()
        {
            if (koreanUiFont != null)
            {
                return koreanUiFont;
            }

            koreanUiFont = Font.CreateDynamicFontFromOSFont(KoreanUiFontNames, 16);
            if (koreanUiFont == null && !warnedMissingKoreanUiFont)
            {
                warnedMissingKoreanUiFont = true;
                Debug.LogWarning("[MainRecordingSettingsWindow] 한글 UI 폰트를 찾지 못했습니다. OS 한글 폰트 설치 상태를 확인하세요.");
            }

            return koreanUiFont;
        }

        private static bool ContainsKorean(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (char c in text)
            {
                if (c >= 0xAC00 && c <= 0xD7A3)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsBrokenTextMarker(string text)
        {
            return text.IndexOf('\uFFFD') >= 0 || text.Contains("??");
        }
    }

    [InitializeOnLoad]
    internal static class MainRecordingEditorPlayModeGuard
    {
        private const string BurstDisableCompilationEnvironmentVariable = "UNITY_BURST_DISABLE_COMPILATION";
        private const string BurstDisableCompilationValue = "1";
        private const string BurstDisableCleanCompileSessionKey =
            "MainRecordingEditorPlayModeGuard.BurstDisableCleanCompileRequested";
        private const string GuardPolicy =
            "Main_Recoding Editor Play Mode sets UNITY_BURST_DISABLE_COMPILATION=1 and requests one clean script compilation to avoid Burst direct-call initializer failures; Unity Playmode tint is neutralized while Play Mode is active.";
        private static readonly Color NeutralPlayModeTint = Color.white;
        private static bool hasSavedPlayModeTint;
        private static Color savedPlayModeTint;
        private static bool hasSavedBurstCompilation;
        private static bool savedBurstCompilation;
        private static bool hasSavedBurstDisableEnvironment;
        private static string savedBurstDisableEnvironmentValue;

        static MainRecordingEditorPlayModeGuard()
        {
            if (ShouldMaintainEditorPlayModeGuard(
                    SceneManager.GetActiveScene().path,
                    Application.isBatchMode,
                    EditorApplication.isPlayingOrWillChangePlaymode))
            {
                ApplyBeforeMainRecordingPlayMode();
            }
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= MaintainEditModeGuard;
            EditorApplication.update += MaintainEditModeGuard;
        }

        private static void MaintainEditModeGuard()
        {
            if (ShouldMaintainEditorPlayModeGuard(
                    SceneManager.GetActiveScene().path,
                    Application.isBatchMode,
                    EditorApplication.isPlayingOrWillChangePlaymode))
            {
                ApplyBeforeMainRecordingPlayMode();
                return;
            }

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                RestoreEditorPlayModeState();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (ShouldApplyEditorPlayModeGuard(SceneManager.GetActiveScene().path, Application.isBatchMode, state))
            {
                ApplyBeforeMainRecordingPlayMode();
                return;
            }

            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                RestoreEditorPlayModeState();
            }
        }

        private static bool ShouldApplyEditorPlayModeGuard(
            string scenePath,
            bool isBatchMode,
            PlayModeStateChange playModeState)
        {
            return !isBatchMode &&
                   (playModeState == PlayModeStateChange.ExitingEditMode ||
                    playModeState == PlayModeStateChange.EnteredPlayMode) &&
                   MainRecordingSettingsWindow.ShouldOpenForScene(scenePath);
        }

        private static bool ShouldMaintainEditorPlayModeGuard(
            string scenePath,
            bool isBatchMode,
            bool isPlayingOrWillChangePlaymode)
        {
            return !isBatchMode &&
                   MainRecordingSettingsWindow.ShouldOpenForScene(scenePath);
        }

        private static void ApplyBeforeMainRecordingPlayMode()
        {
            SaveEditorPlayModeStateBeforeGuard();
            EnsureBurstDirectCallCompilationDisabledForScene(SceneManager.GetActiveScene().path);
            TrySetBurstCompilation(false);

            if (!hasSavedPlayModeTint && TryGetPlayModeTint(out Color playModeTint))
            {
                savedPlayModeTint = playModeTint;
                hasSavedPlayModeTint = true;
                TrySetPlayModeTint(NeutralPlayModeTint);
            }
        }

        private static void RestoreEditorPlayModeState()
        {
            if (hasSavedBurstCompilation)
            {
                TrySetBurstCompilation(savedBurstCompilation);
                hasSavedBurstCompilation = false;
            }

            if (hasSavedBurstDisableEnvironment)
            {
                Environment.SetEnvironmentVariable(
                    BurstDisableCompilationEnvironmentVariable,
                    savedBurstDisableEnvironmentValue,
                    EnvironmentVariableTarget.Process);
                savedBurstDisableEnvironmentValue = null;
                hasSavedBurstDisableEnvironment = false;
            }

            if (hasSavedPlayModeTint)
            {
                TrySetPlayModeTint(savedPlayModeTint);
                hasSavedPlayModeTint = false;
            }
        }

        private static void SaveEditorPlayModeStateBeforeGuard()
        {
            if (!hasSavedBurstCompilation && TryGetBurstCompilation(out bool burstCompilation))
            {
                savedBurstCompilation = burstCompilation;
                hasSavedBurstCompilation = true;
            }

            if (!hasSavedBurstDisableEnvironment)
            {
                savedBurstDisableEnvironmentValue = Environment.GetEnvironmentVariable(
                    BurstDisableCompilationEnvironmentVariable,
                    EnvironmentVariableTarget.Process);
                hasSavedBurstDisableEnvironment = true;
            }
        }

        private static bool ShouldApplyEditorPlayModeGuardForTests(
            string scenePath,
            bool isBatchMode,
            PlayModeStateChange playModeState)
        {
            return ShouldApplyEditorPlayModeGuard(scenePath, isBatchMode, playModeState);
        }

        private static bool ShouldMaintainEditorPlayModeGuardForTests(
            string scenePath,
            bool isBatchMode,
            bool isPlayingOrWillChangePlaymode)
        {
            return ShouldMaintainEditorPlayModeGuard(scenePath, isBatchMode, isPlayingOrWillChangePlaymode);
        }

        private static string GetBurstDisableEnvironmentVariableNameForTests()
        {
            return BurstDisableCompilationEnvironmentVariable;
        }

        private static bool IsBurstDisableEnvironmentValueForTests(string value)
        {
            return IsBurstDisableEnvironmentValue(value);
        }

        private static bool ShouldRequestBurstDisableCleanCompilationForTests(
            string environmentValue,
            bool cleanCompileAlreadyRequested,
            bool isBatchMode)
        {
            return ShouldRequestBurstDisableCleanCompilation(
                environmentValue,
                cleanCompileAlreadyRequested,
                isBatchMode);
        }

        private static string GetEditorPlayModeGuardPolicyForTests()
        {
            return GuardPolicy;
        }

        private static bool CanReflectBurstCompilerOptionsForTests()
        {
            return TryGetBurstCompilation(out _);
        }

        private static bool CanReflectPlayModeTintForTests()
        {
            return TryGetPlayModeTint(out _);
        }

        private static bool IsNeutralPlayModeTintForTests(Color color)
        {
            return Mathf.Abs(color.r - 1f) <= 0.0001f &&
                   Mathf.Abs(color.g - 1f) <= 0.0001f &&
                   Mathf.Abs(color.b - 1f) <= 0.0001f &&
                   Mathf.Abs(color.a - 1f) <= 0.0001f;
        }

        private static bool GetCurrentBurstCompilationForTests()
        {
            return TryGetBurstCompilation(out bool enabled) && enabled;
        }

        private static bool ApplyBurstCompilationForTests(bool enabled)
        {
            if (!TryGetBurstCompilation(out bool current))
            {
                return false;
            }

            TrySetBurstCompilation(enabled);
            return current;
        }

        private static Color GetCurrentPlayModeTintForTests()
        {
            return TryGetPlayModeTint(out Color color) ? color : Color.clear;
        }

        private static Color ApplyPlayModeTintForTests(Color color)
        {
            if (!TryGetPlayModeTint(out Color current))
            {
                return Color.clear;
            }

            TrySetPlayModeTint(color);
            return current;
        }

        private static void EnsureBurstDirectCallCompilationDisabledForScene(string scenePath)
        {
            if (Application.isBatchMode || !MainRecordingSettingsWindow.ShouldOpenForScene(scenePath))
            {
                return;
            }

            string environmentValue = Environment.GetEnvironmentVariable(BurstDisableCompilationEnvironmentVariable);
            bool shouldRequestCleanCompile = ShouldRequestBurstDisableCleanCompilation(
                environmentValue,
                SessionState.GetBool(BurstDisableCleanCompileSessionKey, false),
                Application.isBatchMode);

            if (!IsBurstDisableEnvironmentValue(environmentValue))
            {
                Environment.SetEnvironmentVariable(
                    BurstDisableCompilationEnvironmentVariable,
                    BurstDisableCompilationValue,
                    EnvironmentVariableTarget.Process);
            }

            TrySetBurstCompilation(false);
            if (!shouldRequestCleanCompile)
            {
                return;
            }

            SessionState.SetBool(BurstDisableCleanCompileSessionKey, true);
            Debug.Log(MainRecordingSettingsWindow.BurstCleanCompileLogMessage);
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
        }

        private static bool ShouldRequestBurstDisableCleanCompilation(
            string environmentValue,
            bool cleanCompileAlreadyRequested,
            bool isBatchMode)
        {
            return !isBatchMode &&
                   !cleanCompileAlreadyRequested &&
                   !IsBurstDisableEnvironmentValue(environmentValue);
        }

        private static bool IsBurstDisableEnvironmentValue(string value)
        {
            return !string.IsNullOrEmpty(value) && value != "0";
        }

        private static bool TryGetBurstCompilation(out bool enabled)
        {
            enabled = true;
            object options = GetBurstCompilerOptions();
            PropertyInfo property = GetBurstCompilationProperty(options);
            if (options == null || property == null || property.PropertyType != typeof(bool))
            {
                return false;
            }

            enabled = (bool)property.GetValue(options);
            return true;
        }

        private static bool TrySetBurstCompilation(bool enabled)
        {
            object options = GetBurstCompilerOptions();
            PropertyInfo property = GetBurstCompilationProperty(options);
            if (options == null || property == null || property.PropertyType != typeof(bool) || !property.CanWrite)
            {
                return false;
            }

            property.SetValue(options, enabled);
            return true;
        }

        private static object GetBurstCompilerOptions()
        {
            Type burstCompilerType = Type.GetType("Unity.Burst.BurstCompiler, Unity.Burst");
            FieldInfo optionsField = burstCompilerType?.GetField(
                "Options",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            return optionsField?.GetValue(null);
        }

        private static PropertyInfo GetBurstCompilationProperty(object options)
        {
            return options?.GetType().GetProperty(
                "EnableBurstCompilation",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static bool TryGetPlayModeTint(out Color color)
        {
            color = Color.clear;
            object prefColor = GetPlayModeTintPrefColor();
            PropertyInfo property = GetPrefColorProperty(prefColor);
            if (prefColor == null || property == null || property.PropertyType != typeof(Color))
            {
                return false;
            }

            color = (Color)property.GetValue(prefColor);
            return true;
        }

        private static bool TrySetPlayModeTint(Color color)
        {
            object prefColor = GetPlayModeTintPrefColor();
            PropertyInfo property = GetPrefColorProperty(prefColor);
            if (prefColor == null || property == null || property.PropertyType != typeof(Color) || !property.CanWrite)
            {
                return false;
            }

            property.SetValue(prefColor, color);
            return true;
        }

        private static object GetPlayModeTintPrefColor()
        {
            Type hostViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.HostView");
            FieldInfo field = hostViewType?.GetField(
                "kPlayModeDarken",
                BindingFlags.NonPublic | BindingFlags.Static);
            return field?.GetValue(null);
        }

        private static PropertyInfo GetPrefColorProperty(object prefColor)
        {
            return prefColor?.GetType().GetProperty(
                "Color",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }

    [InitializeOnLoad]
    internal static class MainRecordingSettingsWindowPlayModeAutoOpener
    {
        static MainRecordingSettingsWindowPlayModeAutoOpener()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            string scenePath = SceneManager.GetActiveScene().path;
            if (!MainRecordingSettingsWindow.ShouldAutoOpenForPlayMode(scenePath, Application.isBatchMode, state))
            {
                return;
            }

            EditorApplication.delayCall += () => MainRecordingSettingsWindow.OpenForMainRecordingScene();
        }
    }
}
