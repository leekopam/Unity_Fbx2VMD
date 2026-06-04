using System;
using System.Collections.Generic;
using Member_Han.Modules.Graphics;
using UnityEditor;
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
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_recoding.unity";
        private const string WindowTitle = "Onboarding Assistant";
        private const float RailWidth = 86f;
        private const float SidebarWidth = 336f;
        private const float SidebarMargin = 12f;
        private const float MainLeftPadding = 48f;
        private const float TopPadding = 56f;
        private const float CardWidth = 1008f;
        private const float CardHeight = 288f;
        private const float CardGap = 60f;
        private const string VisualAssetPolicy = "Built-in Unity editor icons and procedural IMGUI textures only; no GUI Pack or project image asset dependency.";

        private static readonly Color PageColor = new Color32(239, 243, 247, 255);
        private static readonly Color RailColor = Color.white;
        private static readonly Color SidebarBorderColor = new Color32(207, 216, 226, 255);
        private static readonly Color SidebarHeaderColor = new Color32(241, 242, 247, 255);
        private static readonly Color MutedTextColor = new Color32(150, 156, 164, 255);
        private static readonly Color ActivePurple = new Color32(61, 57, 132, 255);
        private static readonly Color WhiteButtonColor = new Color32(251, 251, 252, 255);

        private static readonly SidebarItemDefinition[] SidebarItems =
        {
            new SidebarItemDefinition("Camera", "Camera Icon", true, true),
            new SidebarItemDefinition("Directional Light", "Light Icon", false, true),
            new SidebarItemDefinition("Character (Inactive)", "Avatar Icon", false, false),
        };

        private static readonly OnboardingCardDefinition[] OnboardingCards =
        {
            new OnboardingCardDefinition(
                "Basic Setup",
                "Import your model, configure motion capture,\nand start setting up the scene.",
                "Get Started",
                new Color32(62, 58, 129, 255),
                new Color32(210, 212, 238, 255),
                0),
            new OnboardingCardDefinition(
                "Interactions Setup",
                "Generate blueprints that let your viewers\ninteract with your character.",
                "Get Started",
                new Color32(234, 63, 151, 255),
                new Color32(255, 214, 237, 255),
                1),
            new OnboardingCardDefinition(
                "Get Connected!",
                "Connect project integrations when the\nfeature wiring is ready.",
                "Get Started",
                new Color32(24, 158, 145, 255),
                new Color32(215, 246, 239, 255),
                2),
        };

        private static readonly Dictionary<string, Texture2D> RoundedTextureCache = new Dictionary<string, Texture2D>();
        private static GUIStyle titleStyle;
        private static GUIStyle sidebarHeaderStyle;
        private static GUIStyle sidebarItemStyle;
        private static GUIStyle sidebarInactiveItemStyle;
        private static GUIStyle cardTitleStyle;
        private static GUIStyle cardBodyStyle;
        private static GUIStyle cardButtonStyle;
        private static GUIStyle railIconStyle;
        private static GUIStyle toolbarIconStyle;
        private static GUIStyle mascotMarkStyle;

        private Vector2 mainScroll;

        [MenuItem("Tools/Graphics/Open Main_recording Settings")]
        public static MainRecordingSettingsWindow OpenForMainRecordingScene()
        {
            var window = GetWindow<MainRecordingSettingsWindow>(true, WindowTitle, true);
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(1040f, 640f);
            window.Show();
            return window;
        }

        public static string GetWindowTitle()
        {
            return WindowTitle;
        }

        public static bool ShouldOpenForScene(string scenePath)
        {
            return string.Equals(NormalizeScenePath(scenePath), MainRecordingScenePath, StringComparison.OrdinalIgnoreCase);
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
            var labels = new string[SidebarItems.Length];
            for (int i = 0; i < SidebarItems.Length; i++)
            {
                labels[i] = SidebarItems[i].Label;
            }

            return labels;
        }

        private static string[] GetOnboardingCardTitlesForTests()
        {
            var titles = new string[OnboardingCards.Length];
            for (int i = 0; i < OnboardingCards.Length; i++)
            {
                titles[i] = OnboardingCards[i].Title;
            }

            return titles;
        }

        private static string[] GetOnboardingCardButtonLabelsForTests()
        {
            var labels = new string[OnboardingCards.Length];
            for (int i = 0; i < OnboardingCards.Length; i++)
            {
                labels[i] = OnboardingCards[i].ButtonLabel;
            }

            return labels;
        }

        private static string GetVisualAssetPolicyForTests()
        {
            return VisualAssetPolicy;
        }

        private static string NormalizeScenePath(string scenePath)
        {
            return string.IsNullOrWhiteSpace(scenePath)
                ? string.Empty
                : scenePath.Replace('\\', '/').Trim();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(1040f, 640f);
        }

        private void OnGUI()
        {
            EnsureStyles();

            var root = new Rect(0f, 0f, position.width, position.height);
            EditorGUI.DrawRect(root, PageColor);

            var railRect = new Rect(0f, 0f, RailWidth, root.height);
            DrawRail(railRect);

            var sidebarRect = new Rect(
                railRect.xMax + SidebarMargin,
                TopPadding,
                SidebarWidth,
                Mathf.Max(360f, root.height - TopPadding - 72f));
            DrawSidebar(sidebarRect, root.height);

            var mainRect = new Rect(
                sidebarRect.xMax + MainLeftPadding,
                76f,
                Mathf.Max(360f, root.width - sidebarRect.xMax - MainLeftPadding - 56f),
                Mathf.Max(360f, root.height - 76f));
            DrawOnboardingArea(mainRect);
        }

        private static void DrawRail(Rect rect)
        {
            EditorGUI.DrawRect(rect, RailColor);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), new Color32(224, 231, 239, 255));

            DrawRailIcon(new Rect(rect.x + 20f, rect.y + 58f, 40f, 40f), "Prefab Icon", "□", true);
            DrawRailIcon(new Rect(rect.x + 20f, rect.y + 144f, 40f, 40f), "d_UnityEditor.SceneHierarchyWindow", "Σ", false);
            DrawRailIcon(new Rect(rect.x + 20f, rect.y + 226f, 40f, 40f), "Lighting", "!", false);

            DrawRoundedRect(new Rect(rect.xMax - 8f, rect.y + 66f, 4f, 40f), ActivePurple, 2f);
            DrawRailIcon(new Rect(rect.x + 22f, rect.yMax - 88f, 40f, 40f), "Animation Icon", "TV", false);
            DrawRailIcon(new Rect(rect.x + 22f, rect.yMax - 42f, 40f, 40f), "SettingsIcon", "...", false);
        }

        private static void DrawRailIcon(Rect rect, string iconName, string fallback, bool active)
        {
            Color oldColor = GUI.color;
            if (active)
            {
                DrawRoundedRect(rect, new Color32(246, 247, 252, 255), 10f);
            }

            GUI.color = active ? ActivePurple : new Color32(112, 122, 132, 255);
            Texture image = EditorGUIUtility.IconContent(iconName).image;
            if (image != null)
            {
                GUI.DrawTexture(new Rect(rect.x + 8f, rect.y + 8f, 24f, 24f), image, ScaleMode.ScaleToFit, true);
            }
            else
            {
                GUI.Label(rect, fallback, railIconStyle);
            }

            GUI.color = oldColor;
        }

        private static void DrawSidebar(Rect rect, float windowHeight)
        {
            DrawRoundedRect(rect, SidebarBorderColor, 8f);
            DrawRoundedRect(Inset(rect, 1f), Color.white, 7f);
            DrawRoundedRect(new Rect(rect.x + 1f, rect.y + 20f, rect.width - 2f, 64f), SidebarHeaderColor, 0f);

            GUI.Label(new Rect(rect.x + 30f, rect.y + 36f, rect.width - 60f, 32f), WindowTitle, sidebarHeaderStyle);

            float y = rect.y + 102f;
            for (int i = 0; i < SidebarItems.Length; i++)
            {
                DrawSidebarItem(new Rect(rect.x + 30f, y, rect.width - 60f, 40f), SidebarItems[i]);
                y += 62f;
            }

            DrawBottomToolbar(new Rect(rect.x + 22f, windowHeight - 58f, rect.width - 44f, 42f));
        }

        private static void DrawSidebarItem(Rect rect, SidebarItemDefinition item)
        {
            Color oldColor = GUI.color;
            GUI.color = item.Enabled ? ActivePurple : MutedTextColor;

            Texture image = EditorGUIUtility.IconContent(item.IconName).image;
            if (image != null)
            {
                GUI.DrawTexture(new Rect(rect.x, rect.y + 6f, 28f, 28f), image, ScaleMode.ScaleToFit, true);
            }
            else
            {
                GUI.Label(new Rect(rect.x, rect.y, 28f, rect.height), item.Active ? "■" : "•", toolbarIconStyle);
            }

            GUI.color = oldColor;
            GUI.Label(
                new Rect(rect.x + 46f, rect.y + 4f, rect.width - 46f, 32f),
                item.Label,
                item.Enabled ? sidebarItemStyle : sidebarInactiveItemStyle);
        }

        private static void DrawBottomToolbar(Rect rect)
        {
            string[] icons = { "+", "-", "▣", "✎", "...", "▯" };
            float x = rect.x;
            for (int i = 0; i < icons.Length; i++)
            {
                float width = i == 4 ? 42f : 36f;
                GUI.Label(new Rect(x, rect.y, width, rect.height), icons[i], toolbarIconStyle);
                x += width + (i == 4 ? 38f : 8f);
            }
        }

        private void DrawOnboardingArea(Rect rect)
        {
            GUI.Label(new Rect(rect.x, rect.y + 8f, rect.width, 66f), WindowTitle, titleStyle);

            var scrollRect = new Rect(rect.x, rect.y + 132f, rect.width, rect.height - 132f);
            float visibleCardWidth = Mathf.Max(520f, Mathf.Min(CardWidth, scrollRect.width - 30f));
            float contentHeight = (CardHeight * OnboardingCards.Length) + (CardGap * (OnboardingCards.Length - 1)) + 26f;
            var viewRect = new Rect(0f, 0f, visibleCardWidth + 16f, contentHeight);

            mainScroll = GUI.BeginScrollView(scrollRect, mainScroll, viewRect, false, true);
            float y = 0f;
            for (int i = 0; i < OnboardingCards.Length; i++)
            {
                DrawOnboardingCard(new Rect(0f, y, visibleCardWidth, CardHeight), OnboardingCards[i]);
                y += CardHeight + CardGap;
            }

            GUI.EndScrollView();
        }

        private void DrawOnboardingCard(Rect rect, OnboardingCardDefinition card)
        {
            DrawRoundedRect(rect, card.BackgroundColor, 14f);
            DrawRoundedRect(new Rect(rect.x + 20f, rect.y + 155f, 210f, 92f), new Color(0f, 0f, 0f, 0.04f), 22f);

            float textWidth = Mathf.Min(510f, rect.width - 72f);
            GUI.Label(new Rect(rect.x + 36f, rect.y + 42f, textWidth, 48f), card.Title, cardTitleStyle);
            Color oldColor = GUI.color;
            GUI.color = card.BodyTextColor;
            GUI.Label(new Rect(rect.x + 36f, rect.y + 106f, textWidth, 92f), card.Body, cardBodyStyle);
            GUI.color = oldColor;

            var buttonRect = new Rect(rect.x + 36f, rect.y + 206f, 176f, 54f);
            if (GUI.Button(buttonRect, card.ButtonLabel, cardButtonStyle))
            {
                ShowNotification(new GUIContent("프로젝트 기능 연결 예정"));
            }

            if (rect.width >= 760f)
            {
                DrawMascot(new Rect(rect.x + rect.width - 382f, rect.y - 86f, 340f, rect.height + 142f), card.MascotVariant);
            }
        }

        private static void DrawMascot(Rect rect, int variant)
        {
            Color hair = variant == 0 ? new Color32(20, 174, 235, 255) :
                variant == 1 ? new Color32(236, 242, 250, 255) :
                new Color32(41, 204, 178, 255);
            Color hairShadow = variant == 0 ? new Color32(20, 100, 214, 210) :
                variant == 1 ? new Color32(180, 190, 205, 220) :
                new Color32(20, 128, 120, 210);
            Color hoodie = variant == 0 ? new Color32(238, 242, 255, 250) :
                variant == 1 ? new Color32(214, 224, 238, 250) :
                new Color32(234, 246, 241, 250);

            DrawRoundedRect(new Rect(rect.x + 74f, rect.y + 230f, 218f, 170f), hoodie, 54f);
            DrawRoundedRect(new Rect(rect.x + 94f, rect.y + 146f, 150f, 132f), new Color32(255, 222, 210, 255), 62f);
            DrawRoundedRect(new Rect(rect.x + 70f, rect.y + 104f, 204f, 128f), hairShadow, 58f);
            DrawRoundedRect(new Rect(rect.x + 58f, rect.y + 84f, 196f, 118f), hair, 56f);
            DrawRoundedRect(new Rect(rect.x + 42f, rect.y + 110f, 58f, 72f), hair, 24f);
            DrawRoundedRect(new Rect(rect.x + 226f, rect.y + 118f, 58f, 72f), hair, 24f);
            DrawRoundedRect(new Rect(rect.x + 118f, rect.y + 174f, 30f, 22f), Color.white, 12f);
            DrawRoundedRect(new Rect(rect.x + 190f, rect.y + 174f, 30f, 22f), Color.white, 12f);
            DrawRoundedRect(new Rect(rect.x + 126f, rect.y + 181f, 14f, 14f), ActivePurple, 8f);
            DrawRoundedRect(new Rect(rect.x + 198f, rect.y + 181f, 14f, 14f), ActivePurple, 8f);
            DrawRoundedRect(new Rect(rect.x + 154f, rect.y + 226f, 40f, 12f), new Color32(242, 120, 145, 255), 7f);

            GUI.Label(new Rect(rect.x + 111f, rect.y + 286f, 140f, 24f), variant == 1 ? "LIVE" : "MODEL", mascotMarkStyle);
        }

        private static void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
            };
            titleStyle.normal.textColor = new Color32(69, 78, 86, 255);

            sidebarHeaderStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 23,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
            };
            sidebarHeaderStyle.normal.textColor = ActivePurple;

            sidebarItemStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
            };
            sidebarItemStyle.normal.textColor = new Color32(36, 43, 51, 255);

            sidebarInactiveItemStyle = new GUIStyle(sidebarItemStyle);
            sidebarInactiveItemStyle.normal.textColor = MutedTextColor;

            cardTitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
            };
            cardTitleStyle.normal.textColor = Color.white;

            cardBodyStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 23,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                clipping = TextClipping.Clip,
            };
            cardBodyStyle.normal.textColor = Color.white;

            cardButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 23,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(14, 14, 14, 14),
                padding = new RectOffset(16, 16, 6, 6),
            };
            Texture2D buttonTexture = GetRoundedTexture(WhiteButtonColor, 9f);
            cardButtonStyle.normal.background = buttonTexture;
            cardButtonStyle.hover.background = buttonTexture;
            cardButtonStyle.active.background = buttonTexture;
            cardButtonStyle.normal.textColor = new Color32(36, 39, 44, 255);
            cardButtonStyle.hover.textColor = new Color32(20, 24, 28, 255);
            cardButtonStyle.active.textColor = ActivePurple;

            railIconStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 27,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
            };

            toolbarIconStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
            };
            toolbarIconStyle.normal.textColor = ActivePurple;

            mascotMarkStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
            };
            mascotMarkStyle.normal.textColor = new Color(0.42f, 0.45f, 0.68f, 0.28f);
        }

        private static Rect Inset(Rect rect, float amount)
        {
            return new Rect(
                rect.x + amount,
                rect.y + amount,
                rect.width - (amount * 2f),
                rect.height - (amount * 2f));
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
            string key = c.r + "_" + c.g + "_" + c.b + "_" + c.a + "_" + Mathf.RoundToInt(radius) + "_" + width + "x" + height;
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

        private struct SidebarItemDefinition
        {
            public SidebarItemDefinition(string label, string iconName, bool active, bool enabled)
            {
                Label = label;
                IconName = iconName;
                Active = active;
                Enabled = enabled;
            }

            public string Label { get; }
            public string IconName { get; }
            public bool Active { get; }
            public bool Enabled { get; }
        }

        private struct OnboardingCardDefinition
        {
            public OnboardingCardDefinition(
                string title,
                string body,
                string buttonLabel,
                Color backgroundColor,
                Color bodyTextColor,
                int mascotVariant)
            {
                Title = title;
                Body = body;
                ButtonLabel = buttonLabel;
                BackgroundColor = backgroundColor;
                BodyTextColor = bodyTextColor;
                MascotVariant = mascotVariant;
            }

            public string Title { get; }
            public string Body { get; }
            public string ButtonLabel { get; }
            public Color BackgroundColor { get; }
            public Color BodyTextColor { get; }
            public int MascotVariant { get; }
        }
    }

    [InitializeOnLoad]
    public static class MainRecordingSettingsWindowPlayModeHook
    {
        static MainRecordingSettingsWindowPlayModeHook()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            string scenePath = SceneManager.GetActiveScene().path;
            if (!MainRecordingSettingsWindow.ShouldOpenForScene(scenePath))
            {
                return;
            }

            EditorApplication.delayCall += () => MainRecordingSettingsWindow.OpenForMainRecordingScene();
        }
    }
}
