using UnityEngine;

namespace Fbx2Vmd.Settings
{
    public static class MainRecordingSettingsLayoutSpec
    {
        public const string WindowTitle = "Onboarding Assistant";
        public const string PopupObjectName = "Main_Recording_Settings_Popup";
        public const string CanvasObjectName = "UI_Canvas";
        public const int ReferenceWidth = 1265;
        public const int ReferenceHeight = 675;
        public const float DefaultDisplayScale = 1.25f;
        public const float MaximumDisplayScale = 1.5f;
        public const float RailWidth = 56f;
        public const float SidebarX = 56f;
        public const float SidebarY = 31f;
        public const float SidebarWidth = 249f;
        public const float SidebarHeight = 644f;
        public const float MainX = 330f;
        public const float TitleY = 60f;
        public const float CardX = 330f;
        public const float CardY = 121f;
        public const float CardWidth = 672f;
        public const float CardHeight = 192f;
        public const float CardGap = 29f;
        public const float CardCornerRadius = 12f;
        public const float CardTextX = 24f;
        public const float CardTitleY = 24f;
        public const float CardBodyY = 76f;
        public const float CardButtonY = 134f;
        public const float CardButtonWidth = 152f;
        public const float CardButtonHeight = 40f;
        public const string VisualAssetPolicy =
            "Clean & Minimalist GUI Pack remains a runtime fallback resource; Electron Web UI must not copy external reference product assets.";
        public const string KoreanTextPolicy =
            "Visible Korean settings UI text must stay UTF-8 readable; no replacement glyphs, question-mark mojibake, or missing Korean font fallback.";
        public const string GuiPackRoot = "Assets/UI/GUIPack-Clean&Minimalist";
        public const string GuiPackSettingsPopupPrefabPath =
            GuiPackRoot + "/Demo/Resources/Popups/Light/Light - Settings.prefab";
        public const string GuiPackRoundedBackgroundPrefabPath =
            GuiPackRoot + "/Demo/Prefabs/UI Elements/Background/Background - Rounded.prefab";
        public const string GuiPackRoundedButtonPrefabPath =
            GuiPackRoot + "/Demo/Prefabs/UI Elements/Button/Button/Rounded/Basic/Filled/Button Rounded - Filled - White.prefab";
        public const string GuiPackInputFieldPrefabPath =
            GuiPackRoot + "/Demo/Prefabs/UI Elements/Input Field/Light/Single/InputField.prefab";
        public const string GuiPackSettingsIconPath =
            GuiPackRoot + "/Demo/Sprites/Icons/Icons/UI/Settings/Settings.png";
        public const string GuiPackHomeIconPath =
            GuiPackRoot + "/Demo/Sprites/Icons/Icons/UI/Navigation/Home.png";
        public const string GuiPackVideoIconPath =
            GuiPackRoot + "/Demo/Sprites/Icons/Icons/UI/Media/Video Cam.png";
        public const string GuiPackInfoIconPath =
            GuiPackRoot + "/Demo/Sprites/Icons/Icons/UI/Basic/Info.png";
        public const string GuiPackMenuIconPath =
            GuiPackRoot + "/Demo/Sprites/Icons/Icons/UI/Navigation/List - Menu.png";
        public const int DisabledCardCount = 2;

        public static Vector2 ReferenceSize => new Vector2(ReferenceWidth, ReferenceHeight);
        public static Vector2 DefaultDisplaySize => ReferenceSize * DefaultDisplayScale;
        public static Vector2 MaximumDisplaySize => ReferenceSize * MaximumDisplayScale;
        public static Color PageColor => new Color32(239, 243, 247, 255);
        public static Color RailColor => Color.white;
        public static Color SidebarColor => Color.white;
        public static Color SidebarHeaderColor => new Color32(241, 242, 247, 255);
        public static Color SidebarBorderColor => new Color32(207, 216, 226, 255);
        public static Color ActiveColor => new Color32(61, 57, 132, 255);
        public static Color MutedTextColor => new Color32(150, 156, 164, 255);
        public static Color ButtonColor => new Color32(251, 251, 252, 255);
        public static Color DisabledButtonColor => new Color32(232, 235, 240, 255);
        public static Color DisabledButtonTextColor => new Color32(116, 124, 132, 255);

        public static readonly string[] RequiredGuiPackAssetPaths =
        {
            GuiPackSettingsPopupPrefabPath,
            GuiPackRoundedBackgroundPrefabPath,
            GuiPackRoundedButtonPrefabPath,
            GuiPackInputFieldPrefabPath,
            GuiPackSettingsIconPath,
            GuiPackHomeIconPath,
            GuiPackVideoIconPath,
            GuiPackInfoIconPath,
            GuiPackMenuIconPath,
        };

        public static readonly string[] KoreanUiTextSamples =
        {
            "FBX 파일 임포트",
            "기능 1",
            "기능 2",
            "FBX 파일을 선택해 프로젝트로 가져오고 모션 캡쳐 설정을 시작합니다",
            "추후 업데이트 예정입니다.",
            "FBX 파일 선택",
            "준비중",
            "시네마토그래피",
            "환경",
        };

        public static readonly MainRecordingSettingsSidebarItemSpec[] SidebarItems =
        {
            new MainRecordingSettingsSidebarItemSpec("Camera 1", "Camera Icon", true, true),
            new MainRecordingSettingsSidebarItemSpec("Environment", "d_PreMatCube", false, true),
            new MainRecordingSettingsSidebarItemSpec("Directional Light", "Light Icon", false, true),
        };

        public static readonly MainRecordingSettingsCardSpec[] Cards =
        {
            new MainRecordingSettingsCardSpec(
                "FBX 파일 임포트",
                "FBX 파일을 선택해 프로젝트로 가져오고 모션 캡쳐 설정을 시작합니다",
                "FBX 파일 선택",
                new Color32(62, 58, 129, 255),
                new Color32(210, 212, 238, 255),
                MainRecordingSettingsActionType.ImportFbx,
                true),
            new MainRecordingSettingsCardSpec(
                "기능 1",
                "추후 업데이트 예정입니다.",
                "준비중",
                new Color32(234, 63, 151, 255),
                new Color32(255, 214, 237, 255),
                MainRecordingSettingsActionType.ComingSoon,
                false),
            new MainRecordingSettingsCardSpec(
                "기능 2",
                "추후 업데이트 예정입니다.",
                "준비중",
                new Color32(24, 158, 145, 255),
                new Color32(215, 246, 239, 255),
                MainRecordingSettingsActionType.ComingSoon,
                false),
        };

    }
}
